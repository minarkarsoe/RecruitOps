# syntax=docker/dockerfile:1
# Public SSR app. Build context is the REPO ROOT (it needs packages/*).
#   docker build -f frontend/public/Dockerfile .

FROM node:22-alpine AS build
WORKDIR /repo

# Where browser-side /api/* calls are forwarded. This MUST be present during `next build`:
# Next evaluates next.config.mjs `rewrites()` once at build time and freezes the result into
# .next/routes-manifest.json, so supplying it only as a runtime environment variable silently
# leaves the localhost fallback baked in. See the comment in frontend/public/next.config.mjs.
ARG API_INTERNAL_URL=http://localhost:5080/api
ENV API_INTERNAL_URL=$API_INTERNAL_URL

COPY package.json ./
COPY packages ./packages
COPY frontend/public ./frontend/public
RUN npm install --workspaces --include-workspace-root
RUN npm run build --workspace @recruitops/public

FROM node:22-alpine AS final
WORKDIR /repo
ENV NODE_ENV=production
COPY --from=build /repo ./
EXPOSE 3000
USER node
CMD ["npm", "run", "start", "--workspace", "@recruitops/public"]
