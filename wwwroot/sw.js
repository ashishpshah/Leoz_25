/*
 * Leoz PWA service worker : makes the whole application usable offline.
 *
 *  - App shell (layout css / js / fonts / icons + offline page) is pre-cached on install.
 *  - Static files (css, js, images, fonts, CDN) : stale-while-revalidate.
 *  - Pages (navigations) and AJAX GET : network first, the last good copy is kept and served offline.
 *  - Read-only POST (actions named Get*) : network first, cached by url + request body.
 *  - Every other POST (Save / Delete ...) made offline is stored in IndexedDB and replayed
 *    automatically when the connection is back (outbox), for the same logged-in user only.
 *  - Logout / login page : all cached pages and data are removed (no data left for the next user).
 *  - Login page : a copy is kept (it holds no user data) so the app still opens on the login screen offline.
 */

const VERSION = 'v4';
const SHELL_CACHE = 'leoz-shell-' + VERSION;
const STATIC_CACHE = 'leoz-static-' + VERSION;
const PAGE_CACHE = 'leoz-pages';
const DATA_CACHE = 'leoz-data';
const LOGIN_CACHE = 'leoz-login';
const KEEP_CACHES = [SHELL_CACHE, STATIC_CACHE, PAGE_CACHE, DATA_CACHE, LOGIN_CACHE];

const PAGE_CACHE_LIMIT = 300;
const DATA_CACHE_LIMIT = 500;
const NETWORK_TIMEOUT_MS = 10000;

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
  '/js/pwa-install.js'
];

const STATIC_EXT = /\.(css|js|mjs|map|png|jpe?g|gif|webp|svg|ico|bmp|woff2?|ttf|eot|otf|json|mp3|mp4|webm)$/i;

// Pages that end the login session on the server (Account and Logout clear the session) : they wipe the per-user caches.
// NOTE : the login page must never be pre-cached / fetched in the background, that would log the user out.
const LOGIN_PATHS = ['/admin/home/account', '/home/login'];
const LOGOUT_PATHS = ['/admin/home/logout'];
// POST that must never be queued (needs the server right now)
const NEVER_QUEUE_PATHS = ['/admin/home/login', '/home/login'];

/* ------------------------------------------------------------------ install / activate */

self.addEventListener('install', (event) => {
  self.skipWaiting();
  event.waitUntil(
    caches.open(SHELL_CACHE).then((cache) =>
      // one by one, so a single failing file cannot fail the whole install
      Promise.all(SHELL_ASSETS.map((url) => cache.add(new Request(url, { cache: 'reload' })).catch(() => {})))
    )
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(keys.filter((k) => KEEP_CACHES.indexOf(k) === -1).map((k) => caches.delete(k))))
      .then(() => self.clients.claim())
  );
});

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

/* jQuery `cache:false` adds ?_=<timestamp> : remove it so the same request finds its cached copy */
function cacheKeyFor(url) {
  const u = new URL(url.href);
  u.searchParams.delete('_');
  u.hash = '';
  return u.href;
}

function isPath(url, list) {
  const p = url.pathname.toLowerCase().replace(/\/+$/, '');
  return list.indexOf(p) > -1;
}

/* ------------------------------------------------------------------ navigations (pages) */

async function handleNavigate(request, url) {
  if (isPath(url, LOGOUT_PATHS)) {
    // explicit logout : remove this user's pages from the device, online or offline
    await clearUserData();
    try { return await fetch(request); }
    catch (e) { return (await loginPage()) || offlinePage(); }
  }

  if (isPath(url, LOGIN_PATHS)) {
    try {
      const response = await fetch(request);
      // redirected : still logged in ("Remember Me") and sent on to the dashboard, keep everything
      if (response.redirected) return response;
      // login page shown : the server cleared the session, clear the per-user data too, keep the login page for offline
      await clearUserData();
      if (response.ok) await putLimited(LOGIN_CACHE, LOGIN_URL, response.clone(), 1);
      return response;
    } catch (e) {
      // offline : the server session was not touched, keep the user's data (queued saves, "Continue offline")
      return (await loginPage()) || offlinePage();
    }
  }

  const key = cacheKeyFor(url);
  const cached = await caches.match(key, { cacheName: PAGE_CACHE });

  const network = fetch(request).then(async (response) => {
    // a redirect to the login page (session expired) must not replace the cached page
    if (response.ok && !response.redirected) {
      await putLimited(PAGE_CACHE, key, response.clone(), PAGE_CACHE_LIMIT);
    }
    return response;
  });

  try {
    return await (cached ? withTimeout(network, NETWORK_TIMEOUT_MS) : network);
  } catch (e) {
    if (cached) return cached;

    // app opened from the home screen ("/" or start_url) : show the dashboard copy
    if (url.pathname === '/' || url.pathname.toLowerCase() === '/admin' || url.pathname.toLowerCase() === '/admin/home') {
      const home = await caches.match(HOME_URL, { cacheName: PAGE_CACHE });
      if (home) return home;
      const login = await loginPage();
      if (login) return login;
    }
    return offlinePage();
  }
}

function loginPage() {
  return caches.match(LOGIN_URL, { cacheName: LOGIN_CACHE });
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
    const response = await fetch(request);
    if (response.ok && !response.redirected) await putLimited(cacheName, key, response.clone(), limit);
    return response;
  } catch (e) {
    const cached = await caches.match(key, { cacheName: cacheName });
    return cached || offlineResponse(request);
  }
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
  const copy = request.clone();
  const lastSegment = (url.pathname.split('/').filter(Boolean).pop() || '').toLowerCase();

  // Read-only POST (GetMatrix, GetProjectByCustomerId, GetProjectDetail ...) : cache by url + body
  if (request.method === 'POST' && lastSegment.indexOf('get') === 0) {
    const body = await copy.arrayBuffer();
    const key = new URL(cacheKeyFor(url));
    key.searchParams.set('__body', await sha256(body));
    return networkFirst(request, DATA_CACHE, DATA_CACHE_LIMIT, key.href);
  }

  try {
    return await fetch(request);
  } catch (e) {
    // offline
    if (isPath(url, NEVER_QUEUE_PATHS)) {
      return jsonResponse({ IsSuccess: false, StatusCode: 0, Message: 'You are offline. Login needs an internet connection.' });
    }

    const userId = await metaGet('userId');
    if (!userId) {
      return jsonResponse({ IsSuccess: false, StatusCode: 0, Message: 'You are offline. Please connect to the internet and login again.' });
    }

    await outboxAdd({
      url: request.url,
      method: request.method,
      headers: pickHeaders(request.headers),
      body: await copy.blob(),
      referrer: request.referrer || '',
      userId: userId,
      createdAt: Date.now(),
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

async function sha256(buffer) {
  const hash = await crypto.subtle.digest('SHA-256', buffer);
  return Array.from(new Uint8Array(hash)).map((b) => b.toString(16).padStart(2, '0')).join('');
}

/* ------------------------------------------------------------------ outbox replay */

let syncing = null;

function replayOutbox() {
  if (!syncing) syncing = doReplay().finally(() => { syncing = null; });
  return syncing;
}

async function doReplay() {
  const userId = await metaGet('userId');
  const items = await outboxAll();
  const result = { sent: 0, failed: 0, loginRequired: false, pending: 0 };

  for (const item of items) {
    // only the user who made the change may send it
    if (!userId || item.userId !== userId) continue;

    let response;
    try {
      response = await fetch(item.url, { method: item.method, headers: item.headers, body: item.body, credentials: 'same-origin', redirect: 'follow' });
    } catch (e) {
      break; // still offline : keep the rest in order
    }

    // session expired : the server redirected to the login page, keep the item for after login
    if (response.redirected && /\/home\/(account|login)/i.test(response.url)) {
      result.loginRequired = true;
      break;
    }

    let ok = response.ok;
    let message = '';
    try {
      const data = await response.clone().json();
      if (data && typeof data.StatusCode !== 'undefined') ok = ok && (data.StatusCode === 1 || data.StatusCode === 10);
      message = data && data.Message || '';
    } catch (e) { }

    if (ok) {
      await outboxDelete(item.id);
      result.sent++;
    } else {
      // the server refused it (validation ...) : move it to the failed list so the user can see it
      item.attempts++;
      item.error = message || ('HTTP ' + response.status);
      await outboxDelete(item.id);
      await failedAdd(item);
      result.failed++;
    }
  }

  if (result.sent > 0) {
    // data changed on the server : old copies are out of date
    await caches.delete(DATA_CACHE);
  }

  result.pending = (await outboxAll()).length;
  await notifyClients(result);
  return result;
}

self.addEventListener('sync', (event) => {
  if (event.tag === SYNC_TAG) event.waitUntil(replayOutbox());
});

/* ------------------------------------------------------------------ messages from pages */

self.addEventListener('message', (event) => {
  const msg = event.data || {};

  if (msg.type === 'SET_USER') {
    event.waitUntil((async () => {
      // only a logged-in page sets the user; the login / logout navigations clear it.
      // (an offline copy of the login page must not forget the user, or the queued saves could never be sent)
      const userId = msg.userId && Number(msg.userId) > 0 ? String(msg.userId) : null;
      if (!userId) return notifyClients();
      const previous = await metaGet('userId');
      if (previous && previous !== userId) await clearPageCaches();
      await metaSet('userId', userId);
      await replayOutbox();
    })());
  }
  else if (msg.type === 'SYNC_NOW') {
    event.waitUntil(replayOutbox());
  }
  else if (msg.type === 'GET_STATUS') {
    event.waitUntil(notifyClients());
  }
  else if (msg.type === 'DISCARD_FAILED') {
    event.waitUntil(failedClear().then(() => notifyClients()));
  }
  else if (msg.type === 'WARM_PAGES' && Array.isArray(msg.urls)) {
    event.waitUntil(warmPages(msg.urls));
  }
});

/* Download the menu pages in the background so they also open offline */
async function warmPages(urls) {
  const cache = await caches.open(PAGE_CACHE);
  const list = urls
    .map((u) => { try { return new URL(u, self.location.origin); } catch (e) { return null; } })
    .filter((u) => u && u.origin === self.location.origin && !/logout|account|login|delete|remove/i.test(u.pathname));

  for (const u of list) {
    const key = cacheKeyFor(u);
    if (await cache.match(key)) continue;
    try {
      const response = await fetch(u.href, { credentials: 'same-origin', headers: { 'Accept': 'text/html' } });
      if (response.redirected) break; // session expired
      if (response.ok && (response.headers.get('Content-Type') || '').indexOf('text/html') > -1)
        await putLimited(PAGE_CACHE, key, response, PAGE_CACHE_LIMIT);
    } catch (e) { break; }
  }
}

async function clearPageCaches() {
  await caches.delete(PAGE_CACHE);
  await caches.delete(DATA_CACHE);
}

async function clearUserData() {
  await clearPageCaches();
  await metaSet('userId', null);
  await notifyClients();
}

async function notifyClients(result) {
  const status = {
    type: 'OUTBOX_STATUS',
    pending: (await outboxAll()).length,
    // login page offline : can the last user keep working with the copies on this device?
    canContinueOffline: !!(await metaGet('userId')) && !!(await caches.match(HOME_URL, { cacheName: PAGE_CACHE })),
    failed: (await failedAll()).map((f) => ({ url: f.url, error: f.error, createdAt: f.createdAt })),
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
const failedAdd = (item) => { const copy = Object.assign({}, item); delete copy.id; return tx('failed', 'readwrite', (s) => s.add(copy)); };
const failedAll = () => tx('failed', 'readonly', (s) => s.getAll()).then((r) => r || []);
const failedClear = () => tx('failed', 'readwrite', (s) => s.clear());
const metaGet = (key) => tx('meta', 'readonly', (s) => s.get(key)).then((v) => v || null);
const metaSet = (key, value) => tx('meta', 'readwrite', (s) => s.put(value, key));
