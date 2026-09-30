# syntax=docker/dockerfile:1
# Build context: web/. The image is the edge: Caddy serves the SPA and proxies /api (deploy/Caddyfile, bind-mounted).
# Base images are pinned by digest; docs/deployment.md section 5 explains how to bump one.
FROM node:24-slim@sha256:0e0ff40c39bc087845bfb27465a0df4ea419520094bc35842ff83dd8cbe6f9b6 AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN --mount=type=cache,target=/root/.npm npm ci
COPY . .
RUN npm run build && rm -rf dist/.vite

FROM caddy:2.10-alpine@sha256:4c6e91c6ed0e2fa03efd5b44747b625fec79bc9cd06ac5235a779726618e530d
# The official caddy binary carries cap_net_bind_service, so a non-root user can still bind 80 and 443.
RUN addgroup -S -g 10001 caddy && adduser -S -D -H -u 10001 -G caddy caddy && chown -R caddy:caddy /data /config
COPY --from=build /app/dist /srv
USER caddy
