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
                "/api": {target, changeOrigin: true},
                "/socket": {target, ws: true}
            }
        },
        plugins: [react()],
        define: {
            // remotedev will throw an exception without this.
            global: {}
        }
    })
}
