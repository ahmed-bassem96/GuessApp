import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    // The HTTPS backend launch profile also listens on HTTP port 5139.
    proxy: { '/api': 'http://localhost:5139' },
  },
});
