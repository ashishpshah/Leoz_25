/*
 * Leoz PWA service worker : makes the whole application usable offline.
 *
 *  - App shell (layout css / js / fonts / icons + offline page) is pre-cached on install.
 *  - Static files (css, js, images, fonts, CDN) : stale-while-revalidate.
 *  - Pages (navigations) and AJAX GET : network first, the last good copy is kept and served offline.
 *  - Read-only POST (actions named Get*) : network first, cached by url + request body.
 *  - Every other POST (Save / Delete ...) made offline is stored in IndexedDB and replayed
 *    automatically when the connection is back (outbox), for the same logged-in user only.
 *    Each change has an operation id (header X-Leoz-Operation-Id) : the server saves it only once (OfflineOperationFilter),
 *    so a retry never creates a duplicate. A change refused by the server is kept on the device with its reason
 *    (status / rules : js/offline-sync-rules.js) until the user retries or discards it.
 *  - Offline login : after a successful online login a PBKDF2 hash of the password is kept on the device (never the password),
 *    so the same user can login offline. Saved pages are only shown inside a valid offline session :
 *    Remember Me = 14 days sliding, otherwise 30 minutes idle (same as the server). Logout / expired session = login screen.
 *  - Another user logging in online : the previous user's saved pages are removed.
 *  - Login page : a copy is kept (downloaded without cookies, it holds no user data) so the app always opens offline.
 */

importScripts('/js/offline-sync-rules.js'); // self.LeozSyncRules
const Rules = self.LeozSyncRules;

const VERSION = 'v9';
const SHELL_CACHE = 'leoz-shell-' + VERSION;
const STATIC_CACHE = 'leoz-static-' + VERSION;
const PAGE_CACHE = 'leoz-pages-2';
const DATA_CACHE = 'leoz-data-2';
const LOGIN_CACHE = 'leoz-login';
const KEEP_CACHES = [SHELL_CACHE, STATIC_CACHE, PAGE_CACHE, DATA_CACHE, LOGIN_CACHE];

const PAGE_CACHE_LIMIT = 300;
const DATA_CACHE_LIMIT = 4000;
const WARM_MAX_BYTES = 10 * 1024 * 1024; // background download : skip bigger files (documents)
const NETWORK_TIMEOUT_MS = 10000;

const IDLE_MINUTES = 30;              // same as DistributedSession:IdleTimeoutMinutes
const REMEMBER_DAYS = 14;             // same as the "Remember Me" cookie (Program.cs)
const PBKDF2_ITERATIONS = 210000;
const MAX_OFFLINE_LOGIN_FAILS = 5;    // then an online login is required

const HOME_URL = '/Admin/Home/Index';
const LOGIN_URL = '/Admin/Home/Account';
const OFFLINE_URL = '/offline.html';
const SYNC_TAG = 'leoz-outbox-sync';

const SHELL_ASSETS = [
  OFFLINE_URL,
  '/manifest.json',
  '/favicon.ico',
  '/assets/images/logo.webp',
  '/assets/images/LeozLogo.jpeg',
  '/assets/images/leozlogo.jpeg',
  '/assets/images/auth-img.jpg',
  '/assets/images/pwa-icon-192.png',
  '/assets/images/pwa-icon-512.png',
  '/assets/vendor/sweetalert2/sweetalert2.min.css',
  '/assets/vendor/select2/css/select2.min.css',
  '/assets/vendor/datatables.net-bs5/css/dataTables.bootstrap5.min.css',
  '/assets/vendor/datatables.net-responsive-bs5/css/responsive.bootstrap5.min.css',
  '/assets/vendor/datatables.net-fixedcolumns-bs5/css/fixedColumns.bootstrap5.min.css',
  '/assets/vendor/datatables.net-fixedheader-bs5/css/fixedHeader.bootstrap5.min.css',
  '/assets/vendor/datatables.net-buttons-bs5/css/buttons.bootstrap5.min.css',
  '/assets/vendor/datatables.net-select-bs5/css/select.bootstrap5.min.css',
  '/assets/js/config.js',
  '/assets/css/app.min.css',
  '/assets/css/icons.min.css',
  '/assets/fonts/remixicon.woff2?t=1681573354175',
  '/assets/fonts/bootstrap-icons.woff2?1fa40e8900654d2863d011707b9fb6f2',
  '/assets/fonts/materialdesignicons-webfont.woff2?v=7.2.96',
  '/assets/js/vendor.min.js',
  '/assets/vendor/sweetalert2/sweetalert2.min.js',
  '/assets/vendor/select2/js/select2.min.js',
  '/assets/vendor/datatables.net/js/jquery.dataTables.min.js',
  '/assets/vendor/datatables.net-bs5/js/dataTables.bootstrap5.min.js',
  '/assets/vendor/datatables.net-responsive/js/dataTables.responsive.min.js',
  '/assets/vendor/datatables.net-responsive-bs5/js/responsive.bootstrap5.min.js',
  '/assets/vendor/datatables.net-fixedcolumns-bs5/js/fixedColumns.bootstrap5.min.js',
  '/assets/vendor/datatables.net-fixedheader/js/dataTables.fixedHeader.min.js',
  '/assets/vendor/datatables.net-buttons/js/dataTables.buttons.min.js',
  '/assets/vendor/datatables.net-buttons-bs5/js/buttons.bootstrap5.min.js',
  '/assets/vendor/datatables.net-buttons/js/buttons.html5.min.js',
  '/assets/vendor/datatables.net-buttons/js/buttons.flash.min.js',
  '/assets/vendor/datatables.net-buttons/js/buttons.print.min.js',
  '/assets/vendor/datatables.net-keytable/js/dataTables.keyTable.min.js',
  '/assets/vendor/datatables.net-select/js/dataTables.select.min.js',
  '/assets/js/app.js',
  '/assets/js/app.min.js',
  '/common.js',
  '/js/site.js',
  '/css/pwa-install.css',
  '/js/pwa-install.js',
  '/js/pwa-offline-warm.js',
  '/js/offline-sync-rules.js'
];

const STATIC_EXT = /\.(css|js|mjs|map|png|jpe?g|gif|webp|svg|ico|bmp|woff2?|ttf|eot|otf|json|mp3|mp4|webm)$/i;

// Login page shown / Logout : the login session has ended (saved pages stay on the device, locked until the next login)
const LOGIN_PATHS = ['/admin/home/account', '/home/login'];
const LOGOUT_PATHS = ['/admin/home/logout'];
// Login POST : never queued, checked against the offline account when there is no internet
const LOGIN_POST_PATHS = ['/admin/home/login', '/home/login'];
// Change password : needs the server (never queued); afterwards the user must login again with the new password
const CHANGE_PASSWORD_PATHS = ['/admin/changepassword/save'];
const HOME_ALIASES = ['', '/admin', '/admin/home', '/admin/home/index'];

/* ------------------------------------------------------------------ install / activate */

self.addEventListener('install', (event) => {
  self.skipWaiting();
  event.waitUntil(Promise.all([
    caches.open(SHELL_CACHE).then((cache) =>
      // one by one, so a single failing file cannot fail the whole install
      Promise.all(SHELL_ASSETS.map((url) => cache.add(new Request(url, { cache: 'reload' })).catch(() => {})))
    ),
    cacheLoginPage()
  ]));
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(keys.filter((k) => KEEP_CACHES.indexOf(k) === -1).map((k) => caches.delete(k))))
      .then(() => migrateFailedStore().catch(() => { }))
      .then(() => self.clients.claim())
  );
});

/* Older versions moved refused changes to a separate "failed" list (removed with "OK, remove from list") :
   bring them back into the outbox, marked as failed, so the user can retry or discard them */
async function migrateFailedStore() {
  const old = await failedAll();
  for (const f of old) {
    const item = Object.assign({}, f);
    delete item.id;
    item.operationId = item.operationId || newOperationId();
    item.status = 'failed';
    item.category = item.category || 'validation';
    item.lastError = item.lastError || item.error || '';
    item.describe = item.describe || Rules.describeChange(item.url, {});
    await outboxAdd(item);
  }
  if (old.length) await failedClear();
}

function newOperationId() {
  if (self.crypto && crypto.randomUUID) return crypto.randomUUID();
  const b = crypto.getRandomValues(new Uint8Array(16));
  b[6] = (b[6] & 0x0f) | 0x40; b[8] = (b[8] & 0x3f) | 0x80;
  const h = Array.from(b).map((x) => x.toString(16).padStart(2, '0')).join('');
  return h.slice(0, 8) + '-' + h.slice(8, 12) + '-' + h.slice(12, 16) + '-' + h.slice(16, 20) + '-' + h.slice(20);
}

/* ------------------------------------------------------------------ fetch routing */

self.addEventListener('fetch', (event) => {
  const request = event.request;
  const url = new URL(request.url);

  if (url.protocol !== 'http:' && url.protocol !== 'https:') return;

  const sameOrigin = url.origin === self.location.origin;

  if (request.method !== 'GET') {
    if (sameOrigin) event.respondWith(handleNonGet(request, url));
    return;
  }

  if (!sameOrigin) {
    // CDN css / fonts / scripts / images (Google fonts, ionicons ...)
    if (['style', 'font', 'script', 'image'].indexOf(request.destination) > -1)
      event.respondWith(staleWhileRevalidate(request, STATIC_CACHE));
    return;
  }

  if (url.pathname === '/sw.js') return;

  if (request.mode === 'navigate') {
    event.respondWith(handleNavigate(request, url));
    return;
  }

  // background download of a page (pwa-offline-warm.js)
  if (request.headers.get('X-Leoz-Warm') === 'page') {
    event.respondWith(warmPage(request, url));
    return;
  }

  if (isStatic(request, url)) {
    event.respondWith(staleWhileRevalidate(request, STATIC_CACHE));
    return;
  }

  // AJAX GET : partial views, json, files ...
  event.respondWith(networkFirst(request, DATA_CACHE, DATA_CACHE_LIMIT, cacheKeyFor(url)));
});

function isStatic(request, url) {
  return ['style', 'script', 'font', 'image', 'manifest'].indexOf(request.destination) > -1 || STATIC_EXT.test(url.pathname);
}

/*
 * Cache key : the same request is built in different ways by the pages, so the key is normalised :
 *  - path in lower case, parameters sorted, names in lower case
 *  - jQuery `cache:false` ?_=<timestamp> and the DataTables counter (sEcho / draw) are removed
 *  - default values ('', undefined, null, 0, false) are removed : the actions use the same defaults
 *  - DataTables server-side GetData : only the parameters that change the result
 *  - extra : form fields of a read-only POST
 */
const IGNORE_PARAMS = ['_', 'secho', 'draw'];
const DEFAULT_VALUES = ['', 'undefined', 'null', '0', 'false'];
const DATATABLE_PARAMS = ['customerid', 'projectid', 'ssearch', 'isortcol_0', 'ssortdir_0', 'idisplaystart', 'idisplaylength'];

function cacheKeyFor(url, extra) {
  const u = new URL(typeof url === 'string' ? url : url.href, self.location.origin);
  const isDataTable = /\/getdata$/i.test(u.pathname);
  const params = [];
  const add = (k, v) => {
    const name = String(k).toLowerCase();
    const value = String(v);
    if (IGNORE_PARAMS.indexOf(name) > -1 || DEFAULT_VALUES.indexOf(value.toLowerCase()) > -1) return;
    if (isDataTable && DATATABLE_PARAMS.indexOf(name) === -1) return;
    params.push([name, value]);
  };
  u.searchParams.forEach((v, k) => add(k, v));
  (extra || []).forEach((p) => add(p[0], p[1]));
  params.sort((a, b) => (a[0] + '=' + a[1]) < (b[0] + '=' + b[1]) ? -1 : 1);
  const query = params.map((p) => encodeURIComponent(p[0]) + '=' + encodeURIComponent(p[1])).join('&');
  return u.origin + u.pathname.toLowerCase().replace(/\/+$/, '') + (query ? '?' + query : '');
}

function homeKey() { return cacheKeyFor(HOME_URL); }

function isPath(url, list) {
  const p = url.pathname.toLowerCase().replace(/\/+$/, '');
  return list.indexOf(p) > -1;
}

/* ------------------------------------------------------------------ navigations (pages) */

async function handleNavigate(request, url) {
  if (isPath(url, LOGOUT_PATHS)) {
    // logout (online or offline) : lock the saved pages until the next login
    await endOfflineSession();
    try { return await fetch(request); }
    catch (e) { return (await loginPage()) || offlinePage(); }
  }

  if (isPath(url, LOGIN_PATHS)) {
    // the server ended the login (?r=pc password changed, ?r=na user inactive) : the offline login must not work either
    if (url.searchParams.get('r')) await forgetOfflineLogin(url.searchParams.get('r'));

    try {
      const response = await fetch(request);
      // navigations get the redirect itself (opaqueredirect) : still logged in ("Remember Me"), sent on to the dashboard
      if (response.type === 'opaqueredirect' || response.redirected) return response;
      // login page shown : the server has no session any more
      await endOfflineSession();
      if (response.ok) await putLimited(LOGIN_CACHE, LOGIN_URL, response.clone(), 1);
      return response;
    } catch (e) {
      // offline : same as the server, a logged-in user goes on to the dashboard
      if (await hasOfflineSession()) return Response.redirect(HOME_URL, 302);
      return (await loginPage()) || offlinePage();
    }
  }

  const key = cacheKeyFor(url);
  const cached = await caches.match(key, { cacheName: PAGE_CACHE });
  const loggedIn = await hasOfflineSession();

  const network = fetch(request).then(async (response) => {
    // only real pages : a redirect to the login page (session expired) must not replace the cached page
    if (response.ok && response.type === 'basic' && !response.redirected) {
      await putLimited(PAGE_CACHE, key, response.clone(), PAGE_CACHE_LIMIT);
      if (isPath(url, HOME_ALIASES) && key !== homeKey())
        await putLimited(PAGE_CACHE, homeKey(), response.clone(), PAGE_CACHE_LIMIT);
      await touchOfflineSession();
    }
    return response;
  });

  try {
    return await (cached && loggedIn ? withTimeout(network, NETWORK_TIMEOUT_MS) : network);
  } catch (e) {
    // offline : saved pages are only shown inside a valid offline session, otherwise the login screen
    if (!loggedIn) return (await loginPage()) || offlinePage();

    await touchOfflineSession();
    if (cached) return cached;

    // app opened from the home screen ("/" or start_url) : show the dashboard copy
    if (isPath(url, HOME_ALIASES)) {
      const home = await caches.match(homeKey(), { cacheName: PAGE_CACHE });
      if (home) return home;
    }
    return offlinePage();
  }
}

function loginPage() {
  return caches.match(LOGIN_URL, { cacheName: LOGIN_CACHE });
}

/* Login page without cookies : the server sees an anonymous visitor, so this can never log anyone out */
async function cacheLoginPage() {
  try {
    const response = await fetch(LOGIN_URL, { credentials: 'omit', cache: 'no-store' });
    if (response.ok && !response.redirected) await putLimited(LOGIN_CACHE, LOGIN_URL, response, 1);
  } catch (e) { }
}

async function offlinePage() {
  const page = await caches.match(OFFLINE_URL);
  return page || new Response('<h3>You are offline</h3>', { status: 503, headers: { 'Content-Type': 'text/html; charset=utf-8' } });
}

/* ------------------------------------------------------------------ strategies */

async function staleWhileRevalidate(request, cacheName) {
  const cache = await caches.open(cacheName);
  const cached = (await cache.match(request)) || (await caches.match(request)) || (await caches.match(request, { ignoreSearch: true }));

  const network = fetch(request).then((response) => {
    if (response && (response.ok || response.type === 'opaque')) cache.put(request, response.clone()).catch(() => {});
    return response;
  }).catch(() => null);

  if (cached) return cached;
  const response = await network;
  return response || offlineResponse(request);
}

async function networkFirst(request, cacheName, limit, key) {
  try {
    const response = await checkServerLogout(await fetch(request));
    const tooBig = request.headers.get('X-Leoz-Warm') && Number(response.headers.get('Content-Length') || 0) > WARM_MAX_BYTES;
    if (response.ok && !response.redirected && !tooBig) await putLimited(cacheName, key, response.clone(), limit);
    return response;
  } catch (e) {
    const cached = await caches.match(key, { cacheName: cacheName });
    return cached ? withCurrentEcho(cached, request.url) : offlineResponse(request);
  }
}

/* DataTables ignores an answer whose sEcho / draw is older than its own counter : give the saved answer the current one */
async function withCurrentEcho(response, url) {
  const params = new URL(url).searchParams;
  const echo = params.get('sEcho') || params.get('draw');
  if (!echo || (response.headers.get('Content-Type') || '').indexOf('json') === -1) return response;
  try {
    const data = await response.clone().json();
    if ('sEcho' in data) data.sEcho = echo;
    if ('draw' in data) data.draw = Number(echo);
    return jsonResponse(data);
  } catch (e) { return response; }
}

/* Background download of a whole page (pwa-offline-warm.js) : stored like a visited page */
async function warmPage(request, url) {
  const response = await checkServerLogout(await fetch(request));
  const html = (response.headers.get('Content-Type') || '').indexOf('text/html') > -1;

  if (response.ok && !response.redirected && html)
    await putLimited(PAGE_CACHE, cacheKeyFor(url), response.clone(), PAGE_CACHE_LIMIT);

  // the server sends this page somewhere else (e.g. no permission -> dashboard) : do the same offline
  // (never for the login page : that means the session ended)
  if (response.ok && response.redirected && html && !/\/home\/(account|login)/i.test(response.url) && new URL(response.url).origin === self.location.origin)
    await putLimited(PAGE_CACHE, cacheKeyFor(url), Response.redirect(response.url, 302), PAGE_CACHE_LIMIT);

  return response;
}

function withTimeout(promise, ms) {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(new Error('timeout')), ms);
    promise.then((r) => { clearTimeout(timer); resolve(r); }, (e) => { clearTimeout(timer); reject(e); });
  });
}

async function putLimited(cacheName, key, response, limit) {
  try {
    const cache = await caches.open(cacheName);
    await cache.delete(key);
    await cache.put(key, response);
    const keys = await cache.keys();
    // keys() keeps insertion order : drop the oldest
    for (let i = 0; i < keys.length - limit; i++) await cache.delete(keys[i]);
  } catch (e) { }
}

// The fallback MUST always be a Response, otherwise respondWith() fails
function offlineResponse(request) {
  if (wantsJson(request)) {
    return jsonResponse({ IsSuccess: false, StatusCode: 0, Message: 'You are offline and this data is not saved on this device yet.' }, 503);
  }
  return new Response('', { status: 503, statusText: 'Offline', headers: { 'Content-Type': 'text/plain; charset=utf-8' } });
}

function wantsJson(request) {
  const accept = request.headers.get('Accept') || '';
  return accept.indexOf('json') > -1;
}

function jsonResponse(obj, status) {
  return new Response(JSON.stringify(obj), { status: status || 200, headers: { 'Content-Type': 'application/json; charset=utf-8' } });
}

/* ------------------------------------------------------------------ POST / non GET */

async function handleNonGet(request, url) {
  if (request.method === 'POST' && isPath(url, LOGIN_POST_PATHS)) return handleLogin(request);
  if (request.method === 'POST' && isPath(url, CHANGE_PASSWORD_PATHS)) return handleChangePassword(request);

  const copy = request.clone();
  const fieldsCopy = request.clone(); // the form fields describe the change in the "offline changes" list
  const lastSegment = (url.pathname.split('/').filter(Boolean).pop() || '').toLowerCase();

  // Read-only POST (GetMatrix, GetProjectByCustomerId, GetProjectDetail, GetSelectList_User ...) : cache by url + form fields
  if (request.method === 'POST' && lastSegment.indexOf('get') === 0) {
    const fields = [['__post', '1']];
    try { (await copy.formData()).forEach((v, k) => { if (typeof v === 'string') fields.push([k, v]); }); } catch (e) { } // not a form ({} sent as text)
    return networkFirst(request, DATA_CACHE, DATA_CACHE_LIMIT, cacheKeyFor(url, fields));
  }

  try {
    return await checkServerLogout(await fetch(request));
  } catch (e) {
    // offline
    const userId = await metaGet('userId');
    if (!userId) {
      return jsonResponse({ IsSuccess: false, StatusCode: 0, Message: 'You are offline. Please connect to the internet and login again.' });
    }

    const fields = {};
    try { (await fieldsCopy.formData()).forEach((v, k) => { if (typeof v === 'string' && !(k in fields)) fields[k] = v; }); } catch (err) { }

    // passwords are never part of a queued change (password change / login are not queued); only the form as the user sent it
    await outboxAdd({
      operationId: newOperationId(),   // the server saves this change only once, however often it is sent
      url: request.url,
      method: request.method,
      headers: pickHeaders(request.headers),
      body: await copy.blob(),
      referrer: request.referrer || '',
      userId: userId,
      describe: Rules.describeChange(request.url, fields),
      status: 'pending',
      category: null,
      lastError: null,
      createdAt: Date.now(),
      lastTriedAt: null,
      attempts: 0
    });
    await notifyClients();
    try { await self.registration.sync.register(SYNC_TAG); } catch (err) { }

    return queuedResponse(request);
  }
}

function pickHeaders(headers) {
  const out = {};
  ['content-type', 'accept', 'x-requested-with'].forEach((h) => { const v = headers.get(h); if (v) out[h] = v; });
  return out;
}

function queuedResponse(request) {
  const message = 'You are offline. Your changes are saved on this device and will be sent automatically when the internet is back.';
  let back = HOME_URL;
  try {
    const ref = new URL(request.referrer);
    if (ref.origin === self.location.origin) back = ref.pathname + ref.search;
  } catch (e) { }

  if (request.mode === 'navigate') {
    const html = '<!DOCTYPE html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">' +
      '<title>Saved offline - Leoz</title></head><body style="font-family:sans-serif;padding:24px;text-align:center">' +
      '<h3>Saved offline</h3><p>' + message + '</p><p><a href="' + back + '">Continue</a></p></body></html>';
    return new Response(html, { status: 200, headers: { 'Content-Type': 'text/html; charset=utf-8' } });
  }

  // Same shape as the server JsonResult (ResponseModel) : common.js shows the message, then returns to the page
  return jsonResponse({ IsSuccess: true, StatusCode: 1, IsConfirm: true, IsOfflineQueued: true, Message: message, RedirectURL: back });
}


/* ------------------------------------------------------------------ outbox replay */

/* One sync at a time for the whole origin (all tabs share this service worker) */
let syncing = null;

function replayOutbox() {
  if (!syncing) syncing = doReplay().finally(() => { syncing = null; });
  return syncing;
}

async function doReplay() {
  const userId = await metaGet('userId');
  const result = { sent: 0, failed: 0, conflict: 0, loginRequired: false, messages: [] };

  for (const item of await outboxAll()) {
    // only the user who made the change may send it, and only changes that are due (failed / conflict wait for "Retry")
    if (!userId || item.userId !== userId || !Rules.isAutoSyncable(item)) continue;

    item.status = 'syncing';
    item.operationId = item.operationId || newOperationId();
    await outboxPut(item);

    let response = null, networkError = false, json = null;
    try {
      response = await fetch(item.url, {
        method: item.method,
        headers: Object.assign({}, item.headers, { 'X-Leoz-Operation-Id': item.operationId }),
        body: item.body,
        credentials: 'same-origin',
        redirect: 'follow'
      });
    } catch (e) { networkError = true; }

    if (response) {
      await checkServerLogout(response);
      try { json = await response.clone().json(); } catch (e) { }
    }

    const outcome = Rules.classifyReplay({
      networkError: networkError,
      redirectedToLogin: !!(response && response.redirected && /\/home\/(account|login)/i.test(response.url)),
      httpStatus: response ? response.status : 0,
      syncHeader: response ? response.headers.get('X-Leoz-Sync') : null,
      json: json
    });

    // confirmed by the server : it is a normal server record now
    if (outcome.status === 'synced') {
      await outboxDelete(item.id);
      result.sent++;
      continue;
    }

    if (outcome.category !== 'network') item.attempts = (item.attempts || 0) + 1;
    item.status = Rules.nextStatus(outcome, item.attempts || 0);
    item.category = outcome.category;
    item.lastError = outcome.message;
    item.lastTriedAt = Date.now();
    await outboxPut(item); // never removed because of an error : the user decides

    if (item.status === 'failed') { result.failed++; result.messages.push(outcome.message); }
    if (item.status === 'conflict') { result.conflict++; result.messages.push(outcome.message); }
    if (item.status === 'requires_auth') result.loginRequired = true;

    if (outcome.stop) break; // keep the order of the remaining changes
  }

  if (result.sent > 0) {
    // data changed on the server : old copies are out of date (the pages download them again)
    await caches.delete(DATA_CACHE);
  }

  await notifyClients(result);
  return result;
}

/* "Retry" from the offline changes list : failed / conflict changes become due again */
async function retryItems(ids) {
  const userId = await metaGet('userId');
  for (const item of await outboxAll()) {
    if (item.userId !== userId || (ids && ids.indexOf(item.id) === -1)) continue;
    if (item.status !== 'pending') { item.status = 'pending'; item.attempts = 0; await outboxPut(item); }
  }
  return replayOutbox();
}

/* "Discard" : removes only the copy on this device (nothing on the server is deleted), only the user's own change */
async function discardItem(id) {
  const userId = await metaGet('userId');
  const item = (await outboxAll()).find((i) => i.id === id);
  if (item && item.userId === userId && item.status !== 'syncing') await outboxDelete(id);
  await notifyClients();
}

self.addEventListener('sync', (event) => {
  if (event.tag === SYNC_TAG) event.waitUntil(replayOutbox());
});

/* ------------------------------------------------------------------ login : online + offline */

async function handleLogin(request) {
  let userName = '', password = '', remember = false;
  try {
    const form = await request.clone().formData();
    userName = String(form.get('UserName') || '');
    password = String(form.get('Password') || '');
    remember = String(form.get('RememberMe') || '').toLowerCase() === 'true';
  } catch (e) { }

  let response;
  try {
    response = await fetch(request);
  } catch (e) {
    return offlineLogin(userName, password, remember);
  }

  try {
    const data = await response.clone().json();
    if (data && data.StatusCode === 1 && userName && password) await saveOfflineAccount(userName, password, remember);
  } catch (e) { }
  return response;
}

/* Successful online login : keep only a salted PBKDF2 hash of the password (never the password itself) */
/*
 * Change password :
 *  - offline : refused (the old password must be checked by the server; a queued change could be rejected later)
 *  - online, accepted : the offline login now needs the NEW password, and the offline session ("Remember Me") ends,
 *    so the user has to login again (the server does the same : it redirects to Logout, the auth cookie is removed)
 */
async function handleChangePassword(request) {
  let newPassword = '';
  try { newPassword = String((await request.clone().formData()).get('Obj.NewPassword') || ''); } catch (e) { }

  let response;
  try {
    response = await fetch(request);
  } catch (e) {
    return jsonResponse({ IsSuccess: false, StatusCode: 0, Message: 'You are offline. The password can only be changed with an internet connection.' });
  }

  try {
    const data = await response.clone().json();
    if (data && data.StatusCode === 1) {
      const account = await metaGet('account');
      if (account && newPassword) {
        const salt = crypto.getRandomValues(new Uint8Array(16));
        await metaSet('account', { userName: account.userName, salt: toHex(salt), hash: await pbkdf2(newPassword, salt, PBKDF2_ITERATIONS), iterations: PBKDF2_ITERATIONS, fails: 0 });
      } else {
        await metaSet('account', null); // unknown account on this device : no offline login until the next online login
      }
      await endOfflineSession();
    }
  } catch (e) { }
  return response;
}

async function saveOfflineAccount(userName, password, remember) {
  const name = userName.trim().toLowerCase();
  const previous = await metaGet('account');

  // another user on this device : never show the previous user's pages
  if (previous && previous.userName !== name) await clearPageCaches();

  const salt = crypto.getRandomValues(new Uint8Array(16));
  await metaSet('account', {
    userName: name,
    salt: toHex(salt),
    hash: await pbkdf2(password, salt, PBKDF2_ITERATIONS),
    iterations: PBKDF2_ITERATIONS,
    fails: 0
  });
  await startOfflineSession(remember);
}

async function offlineLogin(userName, password, remember) {
  const fail = (message) => jsonResponse({ IsSuccess: false, StatusCode: 0, Message: message });
  const account = await metaGet('account');

  if (!account || account.userName !== userName.trim().toLowerCase())
    return fail('You are offline. This user has not logged in on this device before, please connect to the internet to login.');

  // the server ended the login (BaseController : r=pc / r=na) : only an online login is possible now
  if (account.reset === 'pc')
    return fail('Your password was changed. Please connect to the internet and login with the new password.');
  if (account.reset)
    return fail('Your account is not active. Please connect to the internet or contact the administrator.');

  if (account.fails >= MAX_OFFLINE_LOGIN_FAILS)
    return fail('Too many wrong attempts. Please connect to the internet to login.');

  const hash = await pbkdf2(password, fromHex(account.salt), account.iterations);
  if (!sameHex(hash, account.hash)) {
    account.fails++;
    await metaSet('account', account);
    return fail('User Id and Password does not Match');
  }

  account.fails = 0;
  await metaSet('account', account);
  await startOfflineSession(remember);

  // same shape as the server JsonResult : common.js goes to RedirectURL
  return jsonResponse({ IsSuccess: true, StatusCode: 1, IsOfflineLogin: true, Message: 'Logged in offline', RedirectURL: HOME_URL });
}

async function pbkdf2(password, salt, iterations) {
  const key = await crypto.subtle.importKey('raw', new TextEncoder().encode(password), 'PBKDF2', false, ['deriveBits']);
  const bits = await crypto.subtle.deriveBits({ name: 'PBKDF2', salt: salt, iterations: iterations, hash: 'SHA-256' }, key, 256);
  return toHex(new Uint8Array(bits));
}

function toHex(bytes) { return Array.from(bytes).map((b) => b.toString(16).padStart(2, '0')).join(''); }
function fromHex(hex) { return new Uint8Array((hex.match(/.{2}/g) || []).map((h) => parseInt(h, 16))); }
function sameHex(a, b) {
  if (!a || !b || a.length !== b.length) return false;
  let diff = 0;
  for (let i = 0; i < a.length; i++) diff |= a.charCodeAt(i) ^ b.charCodeAt(i);
  return diff === 0;
}

/* Offline session : saved pages are shown only while it is valid. Remember Me = 14 days, else 30 minutes idle (both sliding) */
function sessionLength(remember) { return remember ? REMEMBER_DAYS * 86400000 : IDLE_MINUTES * 60000; }

/* Session changes run one after another, in the order the events arrived :
   a late "touch" / SET_USER must never bring back a session that a Logout has just ended */
let sessionQueue = Promise.resolve();
function withSessionLock(fn) {
  const run = sessionQueue.then(fn, fn);
  sessionQueue = run.catch(() => { });
  return run;
}

function startOfflineSession(remember) {
  return withSessionLock(() => metaSet('offlineSession', { remember: !!remember, expiresAt: Date.now() + sessionLength(remember) }));
}

async function hasOfflineSession() {
  const s = await metaGet('offlineSession');
  return !!(s && s.expiresAt > Date.now());
}

function touchOfflineSession() {
  return withSessionLock(async () => {
    const s = await metaGet('offlineSession');
    if (s && s.expiresAt > Date.now()) await metaSet('offlineSession', { remember: s.remember, expiresAt: Date.now() + sessionLength(s.remember) });
  });
}

/*
 * Login ended by the server because the password was changed (r=pc, e.g. from the web or another device)
 * or the user was deactivated (r=na) : the old password must not work offline any more, the next login must be online.
 * An inactive user's saved pages are removed too.
 */
async function forgetOfflineLogin(reason) {
  // keep only the user name and the reason (no password hash) : the offline login can tell the user what happened
  const account = await metaGet('account');
  await metaSet('account', account ? { userName: account.userName, reset: reason } : null);
  if (reason === 'na') await clearPageCaches();
  await endOfflineSession();
}

/* AJAX / background requests follow the server redirect themselves : look where they ended */
async function checkServerLogout(response) {
  if (response && response.redirected) {
    const match = /\/home\/account\?(?:.*&)?r=([a-z]+)/i.exec(response.url);
    if (match) await forgetOfflineLogin(match[1].toLowerCase());
  }
  return response;
}

async function endOfflineSession() {
  await withSessionLock(async () => {
    await metaSet('offlineSession', null);
    await metaSet('userId', null);
  });
  await notifyClients();
}

/* ------------------------------------------------------------------ messages from pages */

self.addEventListener('message', (event) => {
  const msg = event.data || {};

  if (msg.type === 'SET_USER') {
    event.waitUntil((async () => {
      // only a logged-in page sets the user; the login / logout navigations clear it.
      // (an offline copy of the login page must not forget the user, or the queued saves could never be sent)
      const userId = msg.userId && Number(msg.userId) > 0 ? String(msg.userId) : null;
      if (!userId) return notifyClients();

      await withSessionLock(async () => {
        // saved pages belong to one user : another user -> remove them
        const owner = await metaGet('cacheOwner');
        if (owner && owner !== userId) await clearPageCaches();
        await metaSet('cacheOwner', userId);
        await metaSet('userId', userId);

        // logged-in page (e.g. restored on the server from the "Remember Me" cookie) : keep an offline session too
        const s = await metaGet('offlineSession');
        if (!s || s.expiresAt <= Date.now() || (msg.remember && !s.remember))
          await metaSet('offlineSession', { remember: !!msg.remember, expiresAt: Date.now() + sessionLength(msg.remember) });
      });

      // logged in again : changes that waited for the login are due now
      for (const item of await outboxAll()) {
        if (item.userId === userId && item.status === 'requires_auth') { item.status = 'pending'; await outboxPut(item); }
      }

      await replayOutbox();
    })());
  }
  else if (msg.type === 'SYNC_NOW') {
    event.waitUntil(replayOutbox());
  }
  else if (msg.type === 'GET_STATUS') {
    event.waitUntil(notifyClients());
  }
  else if (msg.type === 'RETRY_ITEMS') {
    event.waitUntil(retryItems(Array.isArray(msg.ids) ? msg.ids : null));
  }
  else if (msg.type === 'DISCARD_ITEM' && typeof msg.id === 'number') {
    event.waitUntil(discardItem(msg.id));
  }
  else if (msg.type === 'CACHE_LOGIN_PAGE') {
    event.waitUntil(cacheLoginPage());
  }
});

async function clearPageCaches() {
  await caches.delete(PAGE_CACHE);
  await caches.delete(DATA_CACHE);
}

async function notifyClients(result) {
  const account = await metaGet('account');
  const userId = await metaGet('userId');

  // only the current user's changes are shown (never another user's data); without a login only the count
  const all = await outboxAll();
  const mine = userId ? all.filter((i) => i.userId === userId) : [];
  const items = mine.map((i) => ({
    id: i.id,
    describe: i.describe || Rules.describeChange(i.url, {}),
    status: i.status || 'pending',
    label: Rules.statusLabel(i),
    category: i.category || null,
    lastError: i.lastError || null,
    createdAt: i.createdAt,
    lastTriedAt: i.lastTriedAt || null,
    attempts: i.attempts || 0
  }));

  const status = {
    type: 'OUTBOX_STATUS',
    pending: (userId ? mine : all).filter((i) => !Rules.needsAttention(i)).length,
    attention: mine.filter((i) => Rules.needsAttention(i)).length,
    items: items,
    // login page offline : can the last user login with the copies on this device?
    canLoginOffline: !!(account && account.hash && !account.reset && account.fails < MAX_OFFLINE_LOGIN_FAILS),
    result: result || null
  };
  const clients = await self.clients.matchAll({ includeUncontrolled: true, type: 'window' });
  clients.forEach((c) => c.postMessage(status));
}

/* ------------------------------------------------------------------ IndexedDB */

const DB_NAME = 'leoz-offline';
const DB_VERSION = 1;

function openDb() {
  return new Promise((resolve, reject) => {
    const req = indexedDB.open(DB_NAME, DB_VERSION);
    req.onupgradeneeded = () => {
      const db = req.result;
      if (!db.objectStoreNames.contains('outbox')) db.createObjectStore('outbox', { keyPath: 'id', autoIncrement: true });
      if (!db.objectStoreNames.contains('failed')) db.createObjectStore('failed', { keyPath: 'id', autoIncrement: true });
      if (!db.objectStoreNames.contains('meta')) db.createObjectStore('meta');
    };
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
}

async function tx(store, mode, fn) {
  const db = await openDb();
  return new Promise((resolve, reject) => {
    const t = db.transaction(store, mode);
    const result = fn(t.objectStore(store));
    t.oncomplete = () => { db.close(); resolve(result && 'result' in result ? result.result : undefined); };
    t.onerror = () => { db.close(); reject(t.error); };
  });
}

const outboxAdd = (item) => tx('outbox', 'readwrite', (s) => s.add(item));
const outboxAll = () => tx('outbox', 'readonly', (s) => s.getAll()).then((r) => r || []);
const outboxDelete = (id) => tx('outbox', 'readwrite', (s) => s.delete(id));
const outboxPut = (item) => tx('outbox', 'readwrite', (s) => s.put(item));
const failedAll = () => tx('failed', 'readonly', (s) => s.getAll()).then((r) => r || []);
const failedClear = () => tx('failed', 'readwrite', (s) => s.clear());
const metaGet = (key) => tx('meta', 'readonly', (s) => s.get(key)).then((v) => v || null);
const metaSet = (key, value) => tx('meta', 'readwrite', (s) => s.put(value, key));
