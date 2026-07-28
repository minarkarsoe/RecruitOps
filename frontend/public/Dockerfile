# syntax=docker/dockerfile:1
# Public SSR app. Build context is the REPO ROOT (it needs packages/*).
#   docker build -f frontend/public/Dockerfile .

FROM node:22-alpine AS build
WORKDIR /repo
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
