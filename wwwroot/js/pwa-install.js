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

// Register Service Worker
if ('serviceWorker' in navigator) {
    navigator.serviceWorker.register('/sw.js').then(reg => {
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
