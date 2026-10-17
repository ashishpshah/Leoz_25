/*
 * Offline download : once per browser session after login, everything the user can open is downloaded,
 * so the whole app also works offline (menu pages, add / edit forms of every record, Project Detail boxes and records,
 * Daily Update list and forms, Access users, Module Access matrix, city lists ...).
 *
 * Only links and dropdown values that the server rendered for this user are followed : same permissions as online.
 * Only GET requests and the read-only "Get..." POST lookups are made; nothing is ever saved or deleted here.
 * The service worker (sw.js) stores every answer; this file only makes the requests.
 */
(function () {
    if (!('serviceWorker' in navigator) || !window.fetch || !window.DOMParser) return;

    const meta = document.querySelector('meta[name="leoz-user"]');
    const userId = meta && Number(meta.content) > 0 ? meta.content : null;
    if (!userId) return;

    const DONE_KEY = 'pwa_offline_warm_done';
    const CONCURRENCY = 3;
    const MAX_REQUESTS = 3000;
    const DOC_TYPES = ['EPM', 'F3D', 'DRW', 'WCLTSC', 'MB', 'SITE'];

    // actions that change data (all of them are POST) or end the session : never requested
    const UNSAFE = /\/(save|delete|update|subscribe|logout|login|account)[a-z0-9_]*(\?|$)|status/i;
    const LINK = /\/Admin\/[A-Za-z0-9_]+\/[A-Za-z0-9_]+(?:\?[^'"<>\s\\`]*)?/g;

    const seen = new Set();
    const tasks = [];
    let active = 0, count = 0, stopped = false;
    let pages = new Set();

    const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
    const parse = (html) => new DOMParser().parseFromString(html, 'text/html');
    const add = (fn) => { if (!stopped) tasks.push(fn); };

    function progress(done) {
        document.dispatchEvent(new CustomEvent('leoz:offline-warm', { detail: { count: count, done: !!done, stopped: stopped } }));
    }

    async function request(url, options) {
        options = options || {};
        const method = options.method || 'GET';
        const id = method + ' ' + url.toLowerCase() + ' ' + (options.body ? String(options.body) : '');
        if (stopped || seen.has(id) || count >= MAX_REQUESTS) return null;
        seen.add(id);
        count++;
        if (count % 10 === 0) progress(false);

        try {
            const response = await fetch(url, {
                method: method,
                body: options.body,
                credentials: 'same-origin',
                headers: Object.assign({ 'X-Leoz-Warm': options.page ? 'page' : '1' }, options.page ? {} : { 'X-Requested-With': 'XMLHttpRequest' }, options.headers || {})
            });

            // session ended on the server : stop, the next login downloads again
            if (response.redirected && /\/home\/(account|login)/i.test(response.url)) { stopped = true; return null; }
            if (!response.ok) return null;

            const type = response.headers.get('Content-Type') || '';
            if (type.indexOf('json') > -1) return { json: await response.json() };
            if (type.indexOf('html') > -1) return { html: await response.text() };
            return {};
        } catch (e) {
            stopped = true; // offline
            return null;
        }
    }

    // read-only lookups made with POST : same body format as the pages (jQuery form fields)
    function post(url, fields) {
        const options = { method: 'POST' };
        if (fields) {
            options.body = new URLSearchParams(fields).toString();
            options.headers = { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8' };
        }
        return request(url, options);
    }

    /* ------------------------------------------------------------ what a page / form links to */

    function linksIn(doc) {
        const found = new Set();
        const collect = (text) => { if (text && text.indexOf('/Admin/') > -1) (text.match(LINK) || []).forEach((u) => found.add(u)); };

        // attribute values (onclick, href, data-*) are already decoded by the parser; inline scripts are plain text
        doc.querySelectorAll('*').forEach((el) => { for (const a of el.attributes) collect(a.value); });
        doc.querySelectorAll('script').forEach((s) => collect(s.textContent));

        // a url ending with "=" is completed by JavaScript (...?Id=' + id) : those are covered by the rules below
        return Array.from(found).filter((u) => !UNSAFE.test(u) && !/=(&|$)/.test(u));
    }

    function selectValues(doc, id) {
        return Array.from(doc.querySelectorAll('#' + id + ' option')).map((o) => o.value).filter((v) => v && v !== '0');
    }

    async function crawl(url, depth) {
        const isPage = pages.has(url.toLowerCase());
        const result = await request(url, { page: isPage });
        if (!result || !result.html) return;

        const doc = parse(result.html);
        rules(doc, url);

        if (depth >= 2) return;
        linksIn(doc).forEach((link) => {
            if (pages.has(link.toLowerCase())) return; // menu pages are crawled on their own
            add(() => crawl(link, depth + 1));
        });
    }

    /* ------------------------------------------------------------ screens built by JavaScript from dropdowns */

    function rules(doc, url) {
        const path = url.toLowerCase();

        // Project Detail / Daily Update : Customer -> Projects -> project data, boxes, records, daily updates
        if (doc.getElementById('ddlProject') && /projectdetail|dailyupdate/.test(path)) {
            let customers = selectValues(doc, 'ddlCustomer');
            if (customers.length === 0) customers = ['0'];
            const isDaily = path.indexOf('dailyupdate') > -1;
            customers.forEach((c) => add(() => projectsOf(c, isDaily)));
        }

        // Access : Role -> Users -> access form
        if (doc.getElementById('ddlRole') && doc.getElementById('ddlUser'))
            selectValues(doc, 'ddlRole').forEach((r) => add(() => accessUsers(r)));

        // Module Access : matrix of every subject
        if (doc.getElementById('ddlSubject'))
            selectValues(doc, 'ddlSubject').forEach((s) => add(() => post('/Admin/ModuleAccess/GetMatrix', { SubjectCode: s })));

        // Warehouses form : State -> Cities
        if (doc.getElementById('StateId') && doc.getElementById('City_Id'))
            selectValues(doc, 'StateId').forEach((s) => add(() => request('/Admin/Warehouses/GetCity?ParentId=' + s)));
    }

    async function projectsOf(c, isDaily) {
        const result = await post('/Admin/Project/GetProjectByCustomerId?CustomerId=' + c);
        const projects = result && Array.isArray(result.json) ? result.json.map((p) => String(p.Value)) : [];

        projects.forEach((p) => {
            if (isDaily) {
                add(() => dailyUpdates(c, p));
                add(() => request('/Admin/Project/Partial_AddEditForm_DailyUpdate?ProjectId=' + p));
                return;
            }

            add(() => post('/Admin/Project/GetProjectDetail?CustomerId=' + c + '&ProjectId=' + p));
            DOC_TYPES.forEach((t) => add(() => box('/Admin/Project/Partial_AddEditForm_Doc?CustomerId=' + c + '&ProjectId=' + p + '&Type=' + t, c, p)));
            add(() => box('/Admin/Project/Partial_AddEditForm_WP?CustomerId=' + c + '&ProjectId=' + p + '&Type=WPEND', c, p));
            add(() => box('/Admin/Project/Partial_AddEditForm_MP?CustomerId=' + c + '&ProjectId=' + p + '&Type=MPRO', c, p));
            add(() => box('/Admin/Project/Partial_AddEditForm_WCR?ProjectId=' + p + '&Type=WCOMP', c, p));
        });
    }

    // a Project Detail box : its list + the edit form of every record in it (same urls as ProjectDetail.cshtml builds)
    async function box(url, c, p) {
        const result = await request(url);
        if (!result || !result.html) return;
        const html = result.html;
        const ids = (re) => Array.from(new Set(Array.from(html.matchAll(re)).map((m) => m[1]).filter((id) => id !== '0')));

        ids(/fnAddEdit_ProjectSiteDoc\(\s*(\d+)/g).forEach((id) => add(() => request('/Admin/Project/Partial_AddEditForm_Doc?CustomerId=' + c + '&ProjectId=' + p + '&ProjectSiteDocId=' + id)));
        ids(/fnAddEdit_ProjectSiteMP\(\s*(\d+)/g).forEach((id) => add(() => request('/Admin/Project/Partial_AddEditForm_MP?ProjectId=' + p + '&ProjectSiteMatId=' + id)));
        ids(/fnAddEdit_ProjectSiteWP\(\s*(\d+)/g).forEach((id) => add(() => request('/Admin/Project/Partial_AddEditForm_WP?CustomerId=' + c + '&ProjectId=' + p + '&ProjectSitePendingWorkId=' + id)));
        ids(/fnAddEdit_ProjectSiteWCR\(\s*(\d+)/g).forEach((id) => add(() => request('/Admin/Project/Partial_AddEditForm_WCR?ProjectId=' + p + '&AgencyMasterId=' + id)));

        // documents / other links shown in the box
        linksIn(parse(html)).forEach((link) => add(() => crawl(link, 2)));
    }

    // Daily Update list (DataTables server side, first page as the screen loads it) + the edit form of every row
    async function dailyUpdates(c, p) {
        const query = 'CustomerId=' + c + '&ProjectId=' + p + '&sEcho=1&iDisplayStart=0&iDisplayLength=50&iSortCol_0=0&sSortDir_0=asc&sSearch=';
        const result = await request('/Admin/Project/GetData?' + query);
        const rows = result && result.json ? (result.json.aaData || result.json.data || []) : [];
        rows.forEach((row) => {
            if (row && row.Id) add(() => request('/Admin/Project/Partial_AddEditForm_DailyUpdate?Id=' + row.Id + '&ProjectId=' + (row.ProjectId || p)));
        });
    }

    async function accessUsers(roleId) {
        for (const isActive of ['true', 'false']) {
            const result = await post('/Admin/Access/GetSelectList_User', { RoleId: roleId, IsActive: isActive });
            const users = result && Array.isArray(result.json) ? result.json : [];
            users.forEach((u) => { if (u && u.Value && u.Value !== '0') add(() => request('/Admin/Access/Partial_AddEditForm?RoleId=' + roleId + '&UserId=' + u.Value)); });
        }
    }

    /* ------------------------------------------------------------ run */

    async function worker() {
        while (!stopped) {
            const task = tasks.shift();
            if (!task) {
                if (active === 0) return;
                await sleep(100);
                continue;
            }
            active++;
            try { await task(); } catch (e) { }
            active--;
        }
    }

    async function run() {
        try { if (sessionStorage.getItem(DONE_KEY) === userId) return; } catch (e) { }

        await navigator.serviceWorker.ready;
        if (navigator.serviceWorker.controller) navigator.serviceWorker.controller.postMessage({ type: 'CACHE_LOGIN_PAGE' });

        const menu = Array.from(document.querySelectorAll('.side-nav a[href], a.logo[href]'))
            .filter((a) => !a.hasAttribute('data-bs-toggle'))
            .map((a) => a.getAttribute('href'))
            .filter((h) => h && h.charAt(0) === '/' && !UNSAFE.test(h));
        const unique = Array.from(new Set(menu));
        pages = new Set(unique.map((u) => u.toLowerCase()));

        unique.forEach((u) => add(() => crawl(u, 0)));
        progress(false);

        await Promise.all(Array.from({ length: CONCURRENCY }, worker));

        if (!stopped) { try { sessionStorage.setItem(DONE_KEY, userId); } catch (e) { } }
        progress(true);
        console.log('Offline download ' + (stopped ? 'stopped' : 'finished') + ' : ' + count + ' request(s)');
    }

    // after offline changes were sent, the saved copies are out of date : download again on the next page
    navigator.serviceWorker.addEventListener('message', (event) => {
        const msg = event.data || {};
        if (msg.type === 'OUTBOX_STATUS' && msg.result && msg.result.sent > 0) { try { sessionStorage.removeItem(DONE_KEY); } catch (e) { } }
    });

    window.addEventListener('load', () => {
        if (!navigator.onLine) return;
        setTimeout(run, 2000);
    });
})();
