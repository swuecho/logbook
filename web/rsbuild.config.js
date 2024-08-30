import { defineConfig } from '@rsbuild/core';
import { pluginVue2 } from '@rsbuild/plugin-vue2';

import { pluginLess } from "@rsbuild/plugin-less";

export default defineConfig({
    html: {
        template: './public/index.html',
    },
    source: {
        entry: {
            index: './src/main.js',
        },
    },
    output: {
        distPath: {
            root: '../api/wwwroot/',
        }
    },
    plugins: [
        pluginVue2(),
        pluginLess(),
    ],
    server: {
        open: process.platform === 'darwin',
        host: '0.0.0.0',
        port: 9099, // CHANGE YOUR PORT HERE!
        https: false,
        hotOnly: false,
        proxy: {
            "/api/*": {
                target: "http://localhost:5000",
            }
        },
    },
})