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
    let pending = 0;
    let lastFailedCount = 0;

    const pill = document.createElement('div');
    pill.id = 'pwa-net-status';
    pill.className = 'pwa-net-status';
    pill.setAttribute('role', 'status');
    pill.addEventListener('click', () => { if (navigator.onLine) send({ type: 'SYNC_NOW' }); });
    const addPill = () => document.body && document.body.appendChild(pill);
    if (document.body) addPill(); else document.addEventListener('DOMContentLoaded', addPill);

    // Login page : offline, the user who last logged in on this device can login again (checked by the service worker)
    const loginForm = document.getElementById('formLogin');
    let canLoginOffline = false;
    const loginNotice = document.createElement('div');
    loginNotice.className = 'pwa-login-offline';
    if (loginForm) loginForm.parentNode.insertBefore(loginNotice, loginForm);

    function render() {
        const offline = !navigator.onLine;
        let text = '';
        if (offline) text = 'Offline' + (pending > 0 ? ' · ' + pending + ' change(s) saved on device' : (loginForm ? '' : ' · showing saved data'));
        else if (pending > 0 && !loginForm) text = pending + ' offline change(s) waiting · tap to sync';

        pill.textContent = text;
        pill.classList.toggle('is-offline', offline);
        pill.style.display = text ? 'block' : 'none';

        if (loginForm) {
            loginNotice.style.display = offline ? 'block' : 'none';
            loginNotice.innerHTML = '<strong>You are offline.</strong> ' +
                (canLoginOffline ? 'You can login with the account last used on this device and work with the saved data.' : 'Login needs an internet connection.') +
                (pending > 0 ? '<br>' + pending + ' offline change(s) will be sent when the internet is back.' : '');
        }
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
        canLoginOffline = !!msg.canLoginOffline;
        render();

        const r = msg.result;
        if (r && r.sent > 0) notify('success', r.sent + ' offline change(s) sent to the server', 'Reload the page to see the latest data.');
        if (r && r.loginRequired) notify('warning', 'Please login again', 'Your offline changes will be sent after you login.');

        const failed = msg.failed || [];
        if (failed.length > 0 && failed.length !== lastFailedCount && window.Swal) {
            Swal.fire({
                icon: 'error',
                title: failed.length + ' offline change(s) were not accepted by the server',
                html: failed.map(f => '<div style="text-align:left;font-size:13px">• ' + escapeHtml(f.error || '') + '<br><small>' + escapeHtml(f.url) + '</small></div>').join(''),
                confirmButtonText: 'OK, remove from list'
            }).then(() => send({ type: 'DISCARD_FAILED' }));
        }
        lastFailedCount = failed.length;
    });

    function escapeHtml(s) {
        return String(s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    }

    window.addEventListener('online', () => { render(); send({ type: 'SYNC_NOW' }); });
    window.addEventListener('offline', render);

    // who is logged in : also triggers sending of pending offline saves (the login page sends null, which only asks for the status)
    send({ type: 'SET_USER', userId: userId, remember: rememberMe });
    render();

    // Download menu pages once per browser session, so the whole menu also works offline
    window.addEventListener('load', () => {
        if (!userId || !navigator.onLine) return;
        try { if (sessionStorage.getItem('pwa_pages_warmed') === userId) return; } catch (e) { }

        setTimeout(() => {
            const urls = Array.from(document.querySelectorAll('.side-nav a[href], .logo[href]'))
                .filter(a => !a.hasAttribute('data-bs-toggle'))
                .map(a => a.getAttribute('href'))
                .filter(h => h && h.charAt(0) !== '#' && h.indexOf('javascript:') !== 0);
            if (urls.length === 0) return;
            send({ type: 'WARM_PAGES', urls: Array.from(new Set(urls)) });
            try { sessionStorage.setItem('pwa_pages_warmed', userId); } catch (e) { }
        }, 3000);
    });

    // Logout with unsent offline changes : they can only be sent by the same user, so ask first
    document.addEventListener('click', (e) => {
        const link = e.target.closest && e.target.closest('a[href]');
        if (!link || pending === 0 || !/\/home\/logout/i.test(link.getAttribute('href'))) return;
        e.preventDefault();
        const go = () => { window.location.href = link.href; };
        if (window.Swal) {
            Swal.fire({
                icon: 'warning',
                title: pending + ' offline change(s) not sent yet',
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
        } else if (confirm(pending + ' offline change(s) not sent yet. Logout anyway?')) go();
    }, true);
})();
