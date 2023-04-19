import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import {resolve} from 'node:path'

// https://vitejs.dev/config/
export default ({mode}) => {
    process.env = { ...process.env, ...loadEnv(mode, process.cwd(), '') };
    const target = `http://localhost:${process.env.PROXY_PORT}`

    return defineConfig({
        root: process.env.ROOT,
        build: {
            outDir: resolve(process.cwd(),"deploy/public")
        },
        server: {
            port: process.env.PORT,
            proxy: {
                "/api": {target,
                    changeOrigin: true,
                    secure: false,
                    followRedirects: false,
                    xfwd: true,
                    autoRewrite: true,
                    configure: (proxy, _options) => {
                        proxy.on('error', (err, _req, _res) => {
                            console.log('proxy error', err);
                        });
                        proxy.on('proxyReq', (proxyReq, req, _res) => {
                            console.log('Sending Request to the Target:', req.method, req.url);
                        });
                        proxy.on('proxyRes', (proxyRes, req, _res) => {
                            console.log('Received Response from the Target:', proxyRes.statusCode, req.url, JSON.stringify(proxyRes.headers));
                        });
                    },
                },
                "/ws": {target, ws: true}
            }
        },
        plugins: [react()],
        define: {
            // remotedev will throw an exception without this.
            global: {}
        }
    })
}
