import { readFile, writeFile } from 'node:fs/promises';
import { join, relative } from 'node:path';
import { createHash } from 'node:crypto';

export function offlinePlugin() {
  return {
    name: 'logbook-offline',
    setup(api) {
      api.onAfterBuild(async ({ stats }) => {
        const root = api.context.distPath;
        // Use only this compilation's assets, never stale output or server files.
        const builds = stats.stats || [stats];
        const names = [...new Set(builds.flatMap(build => build.compilation.getAssets().map(asset => asset.name)))];
        const files = names.filter(name => !name.endsWith('.map') && name !== 'sw.js').map(name => join(root, name)).sort();
        const hash = createHash('sha256');
        for (const file of files) hash.update(relative(root, file)).update(await readFile(file));
        const cache = `logbook-shell-${hash.digest('hex').slice(0, 16)}`;
        const urls = files.map(file => '/' + relative(root, file).split('\\').join('/'));
        await writeFile(join(root, 'sw.js'), `
const CACHE = ${JSON.stringify(cache)};
const ASSETS = ${JSON.stringify(urls)};
self.addEventListener('install', event => {
  event.waitUntil(caches.open(CACHE).then(cache => cache.addAll(ASSETS.map(url => new Request(url, { cache: 'reload' })))));
});
self.addEventListener('activate', event => {
  event.waitUntil((async () => {
    for (const key of await caches.keys()) {
      if (key.startsWith('logbook-shell-') && key !== CACHE) await caches.delete(key);
    }
    await self.clients.claim();
  })());
});
self.addEventListener('fetch', event => {
  const url = new URL(event.request.url);
  if (event.request.method !== 'GET' || url.origin !== self.location.origin || url.pathname.startsWith('/api/')) return;
  if (event.request.mode === 'navigate' && ['/', '/view', '/calendar', '/content', '/search', '/login', '/logout', '/admin'].includes(url.pathname)) {
    event.respondWith(caches.open(CACHE).then(cache => cache.match('/index.html')).then(cached => cached || fetch(event.request)));
  } else if (ASSETS.includes(url.pathname)) {
    event.respondWith(caches.open(CACHE).then(cache => cache.match(url.pathname)).then(cached => cached || fetch(event.request)));
  }
});
`);
      });
    },
  };
}
