# syntax=docker/dockerfile:1
# Internal SPA. Build context is the REPO ROOT (it needs packages/*).
#   docker build -f frontend/internal/Dockerfile .

FROM node:22-alpine AS build
WORKDIR /repo

# Manifests and the LOCKFILE first, before any source.
#
# ⚠️ `npm ci`, not `npm install`, and the lockfile is why. Until 2026-08-28 this copied only
# `package.json` and ran `npm install`, so the image resolved dependencies afresh on every build
# and the repo's 200 KB `package-lock.json` was ignored outright — what CI tested and what the
# image shipped were free to differ, silently, with nothing to compare. `npm ci` installs the
# locked graph exactly, and fails loudly if the lockfile and manifests disagree.
#
# Every workspace manifest the root `package.json` declares must be present, including
# `frontend/public`, which this image does not build: `npm ci` validates the lockfile against
# the whole workspace graph and errors on a missing one. Copying the manifests alone (not the
# sources) also keeps the install in its own layer, so editing a component does not reinstall
# hundreds of packages.
COPY package.json package-lock.json ./
COPY packages/types/package.json    packages/types/
COPY packages/ui/package.json       packages/ui/
COPY frontend/internal/package.json frontend/internal/
COPY frontend/public/package.json   frontend/public/
RUN npm ci

COPY packages ./packages
COPY frontend/internal ./frontend/internal
RUN npm run build --workspace @recruitops/internal

FROM nginx:1.27-alpine AS final
COPY --from=build /repo/frontend/internal/dist /usr/share/nginx/html
# nginx.conf proxies /api/* to the API container (prevents 405 on POST)
# and falls back to index.html for all other paths (SPA routing).
COPY frontend/internal/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
