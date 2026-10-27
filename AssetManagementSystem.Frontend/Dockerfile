# ============================================================
# 前端镜像（多阶段：node:22 构建 + nginx 提供静态资源与反向代理）
# 构建命令（在前端目录执行）：
#   docker build -t asset-frontend .
# ============================================================
FROM node:22-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:1.27-alpine AS final
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
