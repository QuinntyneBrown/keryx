// Serves a built app (dist/<app>/browser) for the specs in CI, with the single-page fallback the
// router needs: an unknown path answers index.html. Usage: node e2e/static-server.mjs <app> <port>.
// It replaces `ng serve`, which compiles the app a second time after `npm run build` already did.
import { createReadStream, existsSync, statSync } from 'node:fs';
import { createServer } from 'node:http';
import { extname, join, normalize, resolve } from 'node:path';

const [app, port] = process.argv.slice(2);
const root = resolve('dist', app, 'browser');
const types = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json',
  '.svg': 'image/svg+xml',
  '.woff2': 'font/woff2',
  '.ico': 'image/x-icon',
  '.png': 'image/png',
};

createServer((request, response) => {
  const path = decodeURIComponent(new URL(request.url ?? '/', 'http://localhost').pathname);
  let file = normalize(join(root, path));
  if (!file.startsWith(root)) {
    response.writeHead(403).end();
    return;
  }
  if (!existsSync(file) || !statSync(file).isFile()) file = join(root, 'index.html');
  response.writeHead(200, { 'content-type': types[extname(file)] ?? 'application/octet-stream' });
  createReadStream(file).pipe(response);
}).listen(Number(port));
