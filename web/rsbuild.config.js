import { defineConfig } from '@rsbuild/core';
import { offlinePlugin } from './scripts/offline-plugin.js';
import { pluginVue } from '@rsbuild/plugin-vue';

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
    resolve: {
        alias: {
            '@': './src/',
        },
    },
    output: {
        distPath: {
            root: '../api/wwwroot/',
        }
    },
    plugins: [
        pluginVue(),
        offlinePlugin(),
        pluginLess(),
    ],
    server: {
        open: process.platform === 'darwin',
        host: '0.0.0.0',
        port: 9099,
        https: false,
        hotOnly: false,
        proxy: {
            "/api": {
                target: "http://localhost:5000",
            }
        },
    },
})
