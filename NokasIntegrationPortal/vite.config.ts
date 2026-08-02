import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import fs from 'node:fs';
import path from 'node:path';

const isDev = process.env.NODE_ENV !== 'production';

// Vite loads this config before it sets NODE_ENV, so `isDev` is true even during
// `vite build`. Only wire up the dev-server HTTPS certs when they actually exist,
// otherwise a build on a machine without mkcert certs fails here.
const certKey = path.resolve(__dirname, 'cert/key.pem');
const certCrt = path.resolve(__dirname, 'cert/cert.pem');
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
      '@': path.resolve(__dirname, './src'),
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
