/* Kardia — the service worker. It only shows notifications (morning recovery, bedtime, Health Monitor, reports) and
   opens the right screen when one is tapped. Nothing is cached: every page comes straight from the server. */
'use strict';

self.addEventListener('install', () => self.skipWaiting());
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));

self.addEventListener('push', event => {
  let data = {};
  try { data = event.data ? event.data.json() : {}; } catch (e) { data = { body: event.data ? event.data.text() : '' }; }
  event.waitUntil(self.registration.showNotification(data.title || 'Kardia', {
    body: data.body || '',
    tag: data.tag || 'kardia',
    renotify: true,
    icon: '/img/icon-192.png',
    badge: '/img/favicon-32.png',
    data: { url: data.url || '/' },
  }));
});

self.addEventListener('notificationclick', event => {
  event.notification.close();
  const url = new URL((event.notification.data && event.notification.data.url) || '/', self.location.origin).href;
  event.waitUntil(self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then(windows => {
    const open = windows.find(w => w.url.startsWith(self.location.origin));
    if (open) { return open.navigate(url).then(w => (w || open).focus()); }
    return self.clients.openWindow(url);
  }));
});

self.addEventListener('pushsubscriptionchange', event => {
  event.waitUntil(self.clients.matchAll({ type: 'window', includeUncontrolled: true })
    .then(windows => windows.forEach(w => w.postMessage({ type: 'push-resubscribe' }))));
});
