import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

// `__dirname` is not available in an ESM config and is unsupported by Vite's
// native config loader, so derive the project root from import.meta.url instead.
const rootDir = path.dirname(fileURLToPath(import.meta.url));

const isDev = process.env.NODE_ENV !== 'production';

// Vite loads this config before it sets NODE_ENV, so `isDev` is true even during
// `vite build`. Only wire up the dev-server HTTPS certs when they actually exist,
// otherwise a build on a machine without mkcert certs fails here.
const certKey = path.resolve(rootDir, 'cert/key.pem');
const certCrt = path.resolve(rootDir, 'cert/cert.pem');
const hasDevCerts = fs.existsSync(certKey) && fs.existsSync(certCrt);

// Stamps the built bundle so a deployment can be confirmed by viewing page source.
const buildStamp = new Date().toISOString();

const buildStampPlugin = {
  name: 'inject-build-stamp',
  transformIndexHtml(html: string) {
    return html.replace(
      '</head>',
      `  <meta name="build-stamp" content="${buildStamp}" />\n  </head>`,
    );
  },
};

export default defineConfig({
  plugins: [react(), buildStampPlugin],
  resolve: {
    alias: {
      '@': path.resolve(rootDir, './src'),
    },
  },
  ...(isDev && hasDevCerts && {
    server: {
      https: {
        key: fs.readFileSync(certKey),
        cert: fs.readFileSync(certCrt),
      },
      port: 5173,
    },
  }),
  build: {
    outDir: 'dist',
    emptyOutDir: true,
  },
});
