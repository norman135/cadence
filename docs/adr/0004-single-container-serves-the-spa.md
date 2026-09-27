# 0004. A single container serves the API and the SPA

- Status: Accepted
- Date: 2026-09-23

## Context

The React frontend compiles to static files. They could be served by a separate web server container (nginx), a CDN, or the ASP.NET Core app itself. On a small self-hosted server every extra process costs memory, and a separate origin for the API means CORS preflight requests and more complex cookie handling for authentication.

## Decision

The production image contains the compiled SPA in the API's `wwwroot`, and ASP.NET Core serves it:

- `MapStaticAssets` serves assets pre-compressed at publish time (Brotli and gzip) with ETags.
- Vite's content-hashed files under `/assets` are sent with `Cache-Control: public, max-age=31536000, immutable`.
- `index.html` is sent with `Cache-Control: no-cache`, so a new release is picked up on the next navigation.
- Client-side routes fall back to `index.html`. Unknown `/api/*` routes return a JSON 404 instead.

Caddy sits in front for TLS and compression of dynamic responses. In development, Vite's dev server proxies `/api` to the API, so the browser sees a single origin there too.

## Consequences

- One origin: no CORS, and first-party cookies for authentication (M1).
- One application container instead of two, which saves memory and simplifies deployment and versioning. The frontend and backend always ship as a matching pair.
- Static files are served by Kestrel. At the target load this costs little, especially because repeat visits fetch only `index.html` thanks to immutable caching.
