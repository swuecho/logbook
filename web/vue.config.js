module.exports = {
    // https://github.com/vuejs/vue-cli/issues/1040
    runtimeCompiler: true,
    chainWebpack: config => {
        config.module.rules.delete('eslint');
    },
    devServer: {
        open: process.platform === 'darwin',
        host: '0.0.0.0',
        port: 9099, // CHANGE YOUR PORT HERE!
        https: false,
        hotOnly: false,
    },
    outputDir: '../api/wwwroot/'
}