let deferredPrompt;
const pwaBanner = document.getElementById('pwa-install-banner');
const pwaInstallBtn = document.getElementById('pwa-install-btn');
const pwaCloseBtn = document.getElementById('pwa-close-btn');
const pwaIosModal = document.getElementById('pwa-ios-modal');
const pwaHeaderBtn = document.getElementById('pwa-header-install-btn');

// Check if iOS
const isIos = () => {
    const userAgent = window.navigator.userAgent.toLowerCase();
    return /iphone|ipad|ipod/.test(userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
};

// Check if already in installed/standalone mode
const isInStandaloneMode = () => {
    return ('standalone' in window.navigator && window.navigator.standalone) ||
           (window.matchMedia('(display-mode: standalone)').matches);
};

// Register Service Worker (offline support : see /sw.js and the "Offline mode" section at the end of this file)
if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('/sw.js', { updateViaCache: 'none' }).then(reg => {
        console.log('PWA Service Worker registered:', reg.scope);
    }).catch(err => {
        console.warn('Service worker registration failed:', err);
    });
}

// Function to trigger the install prompt
async function triggerInstall() {
    if (isIos()) {
        if (pwaIosModal) {
            pwaIosModal.style.display = pwaIosModal.style.display === 'block' ? 'none' : 'block';
        }
        return;
    }

    if (deferredPrompt) {
        deferredPrompt.prompt();
        const { outcome } = await deferredPrompt.userChoice;
        console.log('Install prompt outcome:', outcome);
        deferredPrompt = null;
        if (pwaBanner) pwaBanner.style.display = 'none';
        if (pwaHeaderBtn) pwaHeaderBtn.style.display = 'none';
    } else {
        // If the browser already has native install icon in address bar
        alert('To install the app, click the Install icon (⭳) in your browser address bar at the top-right!');
    }
}

// Function to show the floating banner
function displayBanner() {
    // If already installed or closed for this session, don't auto-show floating banner
    if (pwaBanner && !isInStandaloneMode() && !sessionStorage.getItem('pwa_banner_dismissed')) {
        pwaBanner.style.display = 'flex';
    }
}

// Listen for browser's beforeinstallprompt
window.addEventListener('beforeinstallprompt', (e) => {
    e.preventDefault();
    deferredPrompt = e;
    displayBanner();
    if (pwaHeaderBtn && !isInStandaloneMode()) {
        pwaHeaderBtn.style.display = 'inline-flex';
    }
});

// Show floating banner on page load
window.addEventListener('load', () => {
    if (isInStandaloneMode()) {
        // Hide all install UI if already running as an installed PWA
        if (pwaBanner) pwaBanner.style.display = 'none';
        if (pwaHeaderBtn) pwaHeaderBtn.style.display = 'none';
        return;
    }

    setTimeout(() => {
        displayBanner();
    }, 800);
});

// Floating banner Install Button
if (pwaInstallBtn) {
    pwaInstallBtn.addEventListener('click', triggerInstall);
}

// Topbar "Install App" Button (Clicking this always re-opens prompt/banner!)
if (pwaHeaderBtn) {
    pwaHeaderBtn.addEventListener('click', () => {
        if (deferredPrompt || isIos()) {
            triggerInstall();
        } else {
            // Re-show the floating banner if closed
            if (pwaBanner) {
                pwaBanner.style.display = 'flex';
            }
        }
    });
}

// Close Button (✕) on Banner
if (pwaCloseBtn) {
    pwaCloseBtn.addEventListener('click', () => {
        if (pwaBanner) pwaBanner.style.display = 'none';
        if (pwaIosModal) pwaIosModal.style.display = 'none';
        // Remember dismissal for this session so it doesn't annoy the user
        sessionStorage.setItem('pwa_banner_dismissed', 'true');
    });
}

// Close Button (✕) on iOS Modal
const pwaIosCloseBtn = document.getElementById('pwa-ios-close-btn');
if (pwaIosCloseBtn) {
    pwaIosCloseBtn.addEventListener('click', () => {
        if (pwaIosModal) pwaIosModal.style.display = 'none';
    });
}

// Hide everything once successfully installed
window.addEventListener('appinstalled', () => {
    if (pwaBanner) pwaBanner.style.display = 'none';
    if (pwaHeaderBtn) pwaHeaderBtn.style.display = 'none';
    if (pwaIosModal) pwaIosModal.style.display = 'none';
    console.log('App successfully installed!');
});

/* ======================================================================
   Offline mode
   - status pill : "Offline" and "N changes waiting to sync"
   - tells the service worker who is logged in, so offline saves are only sent for the same user
   - sends offline saves as soon as the connection is back
   - downloads the menu pages in the background so they also open offline
   - warns before logout when offline saves are not sent yet
   ====================================================================== */
(function () {
    if (!('serviceWorker' in navigator)) return;

    const userMeta = document.querySelector('meta[name="leoz-user"]');
    const userId = userMeta && Number(userMeta.content) > 0 ? userMeta.content : null;
    const rememberMeta = document.querySelector('meta[name="leoz-remember"]');
    const rememberMe = !!(rememberMeta && rememberMeta.content === '1');
    let pending = 0;      // changes waiting to be sent
    let attention = 0;    // changes refused by the server / conflict : the user decides (Retry / Discard)
    let items = [];       // the current user's offline changes (from the service worker)
    let lastAttention = 0;

    const pill = document.createElement('div');
    pill.id = 'pwa-net-status';
    pill.className = 'pwa-net-status';
    pill.setAttribute('role', 'status');
    pill.addEventListener('click', () => {
        if (items.length > 0) showChanges();
        else if (navigator.onLine) send({ type: 'SYNC_NOW' });
    });
    const addPill = () => document.body && document.body.appendChild(pill);
    if (document.body) addPill(); else document.addEventListener('DOMContentLoaded', addPill);

    // Login page : offline, the user who last logged in on this device can login again (checked by the service worker)
    const loginForm = document.getElementById('formLogin');
    let canLoginOffline = false;
    const loginNotice = document.createElement('div');
    loginNotice.className = 'pwa-login-offline';
    if (loginForm) loginForm.parentNode.insertBefore(loginNotice, loginForm);

    // background download for offline use (pwa-offline-warm.js)
    let warming = null;
    document.addEventListener('leoz:offline-warm', (e) => {
        warming = e.detail.done ? null : e.detail.count;
        render();
    });

    function render() {
        const offline = !navigator.onLine;
        let text = '';
        if (attention > 0 && !loginForm) text = attention + ' offline change(s) need attention · tap to view';
        else if (offline) text = 'Offline' + (pending > 0 ? ' · ' + pending + ' change(s) saved on device' : (loginForm ? '' : ' · showing saved data'));
        else if (pending > 0 && !loginForm) text = pending + ' offline change(s) waiting to sync · tap to view';
        else if (warming !== null) text = 'Saving app for offline use… ' + warming;

        pill.textContent = text;
        pill.classList.toggle('is-offline', offline);
        pill.classList.toggle('is-attention', attention > 0 && !loginForm);
        pill.style.display = text ? 'block' : 'none';

        if (loginForm) {
            loginNotice.style.display = offline ? 'block' : 'none';
            loginNotice.innerHTML = '<strong>You are offline.</strong> ' +
                (canLoginOffline ? 'You can login with the account last used on this device and work with the saved data.' : 'Login needs an internet connection.') +
                (pending > 0 ? '<br>' + pending + ' offline change(s) will be sent when the internet is back.' : '');
        }

        renderPendingCard();
    }

    function send(msg) {
        navigator.serviceWorker.ready.then(reg => { if (reg.active) reg.active.postMessage(msg); });
    }

    function notify(icon, title, text) {
        if (window.Swal) Swal.fire({ icon: icon, title: title, text: text || '', timer: icon === 'success' ? 2500 : undefined });
        else alert(title + (text ? '\n' + text : ''));
    }

    navigator.serviceWorker.addEventListener('message', (event) => {
        const msg = event.data || {};
        if (msg.type !== 'OUTBOX_STATUS') return;

        pending = msg.pending || 0;
        attention = msg.attention || 0;
        items = Array.isArray(msg.items) ? msg.items : [];
        canLoginOffline = !!msg.canLoginOffline;
        render();

        // the offline changes list is open : show the new statuses
        if (window.Swal && Swal.isVisible() && document.getElementById('pwa-changes')) showChanges();

        const r = msg.result;
        if (r && r.sent > 0) notify('success', r.sent + ' offline change(s) saved on the server', 'The lists are being refreshed.');
        if (r && r.loginRequired) notify('warning', 'Please login again', 'Your offline changes are kept on this device and will be sent after you login.');

        // refused by the server : explain, keep the changes (no "remove from list")
        if (attention > lastAttention && r && (r.failed > 0 || r.conflict > 0) && window.Swal && !loginForm) {
            Swal.fire({
                icon: 'warning',
                title: (r.failed + r.conflict) + ' offline change(s) were not accepted by the server',
                html: '<div style="text-align:left;font-size:13px">' + (r.messages || []).map(m => '• ' + escapeHtml(m)).join('<br>') +
                    '<br><br>They are <strong>kept on this device</strong>. You can retry them or discard them from the offline changes list.</div>',
                showCancelButton: true,
                confirmButtonText: 'View offline changes',
                cancelButtonText: 'Close'
            }).then(res => { if (res.isConfirmed) showChanges(); });
        }
        lastAttention = attention;
    });

    /* ---- offline changes list : status of every change + Retry / Discard ---- */

    const MODULE_NAMES = { project: 'Project', customer: 'Customer', employee: 'Employee', vendor: 'Vendor', user: 'User', role: 'Role', menu: 'Menu', package: 'Package', warehouses: 'Warehouse', access: 'Access', moduleaccess: 'Module Access', changepassword: 'Password', vendorsubscription: 'Subscription' };
    const ACTION_NAMES = { create: 'New', update: 'Edit', delete: 'Delete', status: 'Status' };

    function moduleName(d) { return MODULE_NAMES[d.module] || (d.module ? d.module.charAt(0).toUpperCase() + d.module.slice(1) : 'Change'); }
    function badgeClass(i) {
        if (i.status === 'failed' || i.status === 'conflict') return 'bg-danger';
        if (i.status === 'requires_auth' || i.status === 'retry') return 'bg-warning text-dark';
        if (i.status === 'syncing') return 'bg-info';
        return 'bg-secondary';
    }
    function when(t) { try { return new Date(t).toLocaleString(); } catch (e) { return ''; } }

    function itemHtml(i, compact) {
        const d = i.describe || {};
        const canRetry = ['failed', 'conflict', 'retry', 'requires_auth'].indexOf(i.status) > -1;
        return '<tr>' +
            '<td><span class="badge ' + badgeClass(i) + '">' + escapeHtml(i.label || i.status) + '</span></td>' +
            '<td><strong>' + escapeHtml((ACTION_NAMES[d.action] || '') + ' ' + moduleName(d)) + '</strong>' + (d.title ? ' : ' + escapeHtml(d.title) : '') +
            '<div class="text-muted small">Saved offline ' + escapeHtml(when(i.createdAt)) + '</div>' +
            (i.lastError && i.status !== 'pending' ? '<div class="small text-danger">' + escapeHtml(i.lastError) + '</div>' : '') + '</td>' +
            (compact ? '' : '<td class="text-nowrap text-end">' +
                (canRetry ? '<button type="button" class="btn btn-sm btn-primary me-1" data-pwa-retry="' + i.id + '">Retry</button>' : '') +
                (i.status !== 'syncing' ? '<button type="button" class="btn btn-sm btn-outline-danger" data-pwa-discard="' + i.id + '">Discard</button>' : '') +
                '</td>') +
            '</tr>';
    }

    function showChanges() {
        if (!window.Swal) return;
        const rows = items.map(i => itemHtml(i, false)).join('');
        Swal.fire({
            title: 'Offline changes',
            width: 760,
            html: '<div id="pwa-changes" style="text-align:left">' +
                (rows ? '<div class="table-responsive"><table class="table table-sm align-middle mb-2"><tbody>' + rows + '</tbody></table></div>'
                    : '<p class="text-muted">No offline changes on this device.</p>') +
                '<p class="small text-muted mb-0">Changes are sent automatically when the internet is back. <strong>Discard</strong> removes only the copy on this device; nothing on the server is deleted.</p></div>',
            showCancelButton: true,
            confirmButtonText: 'Sync now',
            cancelButtonText: 'Close',
            showConfirmButton: navigator.onLine && items.length > 0,
            didOpen: (popup) => {
                popup.addEventListener('click', (e) => {
                    const retry = e.target.closest('[data-pwa-retry]');
                    const discard = e.target.closest('[data-pwa-discard]');
                    if (retry) send({ type: 'RETRY_ITEMS', ids: [Number(retry.getAttribute('data-pwa-retry'))] });
                    if (discard) confirmDiscard(Number(discard.getAttribute('data-pwa-discard')));
                });
            }
        }).then(res => { if (res.isConfirmed) send({ type: 'SYNC_NOW' }); });
    }

    function confirmDiscard(id) {
        const i = items.find(x => x.id === id);
        if (!i) return;
        const d = i.describe || {};
        Swal.fire({
            icon: 'warning',
            title: 'Discard this offline change?',
            html: escapeHtml((ACTION_NAMES[d.action] || '') + ' ' + moduleName(d) + (d.title ? ' : ' + d.title : '')) +
                '<br><br><small>Only the copy saved on this device is removed. It will not be sent to the server. Nothing on the server is deleted.</small>',
            showCancelButton: true,
            confirmButtonText: 'Discard',
            cancelButtonText: 'Keep it'
        }).then(res => {
            if (res.isConfirmed) send({ type: 'DISCARD_ITEM', id: id });
            else showChanges();
        });
    }

    /* ---- list pages : pending offline changes of this module, shown above the list until the server confirms them ---- */

    function renderPendingCard() {
        if (loginForm) return;
        const container = document.querySelector('.content-page .content .container-fluid');
        if (!container) return;

        const parts = location.pathname.split('/').filter(Boolean); // Admin / Project / Index
        const module = (parts[1] || '').toLowerCase();
        const mine = items.filter(i => i.describe && i.describe.module === module);

        let card = document.getElementById('pwa-pending-card');
        if (mine.length === 0) { if (card) card.remove(); return; }

        if (!card) {
            card = document.createElement('div');
            card.id = 'pwa-pending-card';
            card.className = 'card border-warning mt-2 mb-2';
            container.insertBefore(card, container.firstChild);
            card.addEventListener('click', (e) => { if (e.target.closest('[data-pwa-open]')) showChanges(); });
        }

        card.innerHTML = '<div class="card-body py-2">' +
            '<div class="d-flex justify-content-between align-items-center mb-1"><strong>Pending sync (' + mine.length + ')</strong>' +
            '<button type="button" class="btn btn-sm btn-outline-secondary" data-pwa-open="1">Manage</button></div>' +
            '<div class="small text-muted mb-1">Saved on this device, not yet confirmed by the server. They appear in the list after the sync.</div>' +
            '<div class="table-responsive"><table class="table table-sm mb-0"><tbody>' + mine.map(i => itemHtml(i, true)).join('') + '</tbody></table></div></div>';
    }

    function escapeHtml(s) {
        return String(s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    }

    window.addEventListener('online', () => { render(); send({ type: 'SYNC_NOW' }); });
    // app opened / back in front while online : send what is due (the "online" event alone does not prove the server is reachable)
    document.addEventListener('visibilitychange', () => { if (document.visibilityState === 'visible' && navigator.onLine) send({ type: 'SYNC_NOW' }); });
    window.addEventListener('offline', render);

    // who is logged in : also triggers sending of pending offline saves (the login page sends null, which only asks for the status)
    send({ type: 'SET_USER', userId: userId, remember: rememberMe });
    render();

    // Menu pages, forms, data and the login page are downloaded for offline use by pwa-offline-warm.js

    // Logout with unsent offline changes : they can only be sent by the same user, so ask first
    document.addEventListener('click', (e) => {
        const link = e.target.closest && e.target.closest('a[href]');
        const unsent = pending + attention;
        if (!link || unsent === 0 || !/\/home\/logout/i.test(link.getAttribute('href'))) return;
        e.preventDefault();
        const go = () => { window.location.href = link.href; };
        if (window.Swal) {
            Swal.fire({
                icon: 'warning',
                title: unsent + ' offline change(s) not sent yet',
                text: navigator.onLine ? 'Sync now before logout?' : 'They will be sent after you login again with this user. Logout anyway?',
                showCancelButton: true,
                showDenyButton: navigator.onLine,
                confirmButtonText: 'Logout',
                denyButtonText: 'Sync now',
                cancelButtonText: 'Cancel'
            }).then(res => {
                if (res.isConfirmed) go();
                else if (res.isDenied) send({ type: 'SYNC_NOW' });
            });
        } else if (confirm(unsent + ' offline change(s) not sent yet. Logout anyway?')) go();
    }, true);
})();
