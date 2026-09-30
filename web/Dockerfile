# syntax=docker/dockerfile:1
# Build context: web/. The image is the edge: Caddy serves the SPA and proxies /api (deploy/Caddyfile, bind-mounted).
# Base images are pinned by digest; docs/deployment.md section 5 explains how to bump one.
FROM node:24-slim@sha256:0e0ff40c39bc087845bfb27465a0df4ea419520094bc35842ff83dd8cbe6f9b6 AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN --mount=type=cache,target=/root/.npm npm ci
COPY . .
RUN npm run build && rm -rf dist/.vite

FROM caddy:2.11-alpine@sha256:6aeddd44c3078b0f9a35206472a11420648a79c184603ef95957d0a20044cb2b
# The official caddy binary carries cap_net_bind_service, so a non-root user can still bind 80 and 443.
RUN addgroup -S -g 10001 caddy && adduser -S -D -H -u 10001 -G caddy caddy && chown -R caddy:caddy /data /config
COPY --from=build /app/dist /srv
USER caddy
