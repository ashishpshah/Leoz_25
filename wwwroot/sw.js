const CACHE_NAME = 'leoz-pwa-v2';
const ASSETS_TO_CACHE = [
  '/',
  '/favicon.ico',
  '/manifest.json'
];

self.addEventListener('install', (event) => {
  self.skipWaiting();
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) =>
      // cache each asset individually so one failing URL cannot fail the whole install
      Promise.all(ASSETS_TO_CACHE.map((url) => cache.add(url).catch(() => {})))
    )
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(keys.filter((k) => k !== CACHE_NAME).map((k) => caches.delete(k))))
      .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', (event) => {
  const request = event.request;

  // Only handle same-origin GET requests
  if (request.method !== 'GET') return;
  if (new URL(request.url).origin !== self.location.origin) return;

  // Network first. The fallback MUST always resolve to a Response, otherwise
  // respondWith() rejects with "Failed to convert value to 'Response'".
  event.respondWith(
    fetch(request).catch(async () => {
      const cached = await caches.match(request);
      if (cached) return cached;

      if (request.mode === 'navigate') {
        const shell = await caches.match('/');
        if (shell) return shell;
      }

      return new Response('', {
        status: 503,
        statusText: 'Offline',
        headers: { 'Content-Type': 'text/plain; charset=utf-8' }
      });
    })
  );
});
