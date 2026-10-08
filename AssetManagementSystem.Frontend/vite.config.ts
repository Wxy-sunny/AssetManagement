import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import vueDevTools from 'vite-plugin-vue-devtools'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    vue(),
    vueDevTools(),
  ],
  resolve: {
    alias: { 
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5099',   // 后端 HTTP 地址
        changeOrigin: true,                // 修改请求头 origin
        rewrite: (path) => path.replace(/^\/api/, ''), // 去掉 /api 前缀
        secure: false,                     // 忽略证书错误（虽然这里用 http，保留无妨）
        configure: (proxy, options) => {
          // 1. 在转发请求时，强制请求不压缩，避免代理解压问题
          proxy.on('proxyReq', (proxyReq, req, res) => {
            proxyReq.setHeader('Accept-Encoding', 'identity');
            //console.log('[Proxy] 请求:', req.method, req.url);
          });

          // 2. 监听代理响应，打印状态和内容长度
          proxy.on('proxyRes', (proxyRes, req, res) => {
            let body = '';
            proxyRes.on('data', chunk => { body += chunk; });
            proxyRes.on('end', () => {
              //console.log(`[Proxy] 响应状态: ${proxyRes.statusCode}, 内容长度: ${body.length}`);
              if (body.length === 0) {
                console.warn('[Proxy] ⚠️ 响应体为空！');
              }
            });
          });

          // 3. 捕获代理错误
          proxy.on('error', (err, req, res) => {
            console.error('[Proxy] 错误:', err);
          });
        },
      },
      // 资料附件静态资源（后端 wwwroot/uploads），用于图片在线预览
      '/uploads': {
        target: 'http://localhost:5099',
        changeOrigin: true,
      },
    },
  },
})