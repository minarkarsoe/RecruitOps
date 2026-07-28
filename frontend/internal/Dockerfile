# syntax=docker/dockerfile:1
# Internal SPA. Build context is the REPO ROOT (it needs packages/*).
#   docker build -f frontend/internal/Dockerfile .

FROM node:22-alpine AS build
WORKDIR /repo
COPY package.json ./
COPY packages ./packages
COPY frontend/internal ./frontend/internal
RUN npm install --workspaces --include-workspace-root
RUN npm run build --workspace @recruitops/internal

FROM nginx:1.27-alpine AS final
COPY --from=build /repo/frontend/internal/dist /usr/share/nginx/html
# nginx.conf proxies /api/* to the API container (prevents 405 on POST)
# and falls back to index.html for all other paths (SPA routing).
COPY frontend/internal/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
