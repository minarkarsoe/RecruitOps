/** @type {import('next').NextConfig} */
const nextConfig = {
  // Lets the shared workspace packages be transpiled by Next.
  transpilePackages: ['@recruitops/ui', '@recruitops/types'],
  async rewrites() {
    // Browser-side calls are same-origin (`/api/...`) and land here, which forwards them to the
    // API. Server-side rendering does NOT use this path — it calls `API_INTERNAL_URL` directly —
    // which is why the job page could render correctly while every client-side call failed.
    //
    // ⚠️ This destination MUST NOT be hard-coded to localhost. Inside a container `localhost`
    // is the container itself, where nothing listens on 5080. Verified 2026-08-26 from inside
    // `frontend-public`: `localhost:5080` → ECONNREFUSED, `backend:8080` → 200. The symptom was
    // a 500 on every browser-side request, including submitting an application, with
    // `Error: connect ECONNREFUSED 127.0.0.1:5080` in the container log.
    //
    // ⚠️⚠️ AND THE ENV VAR MUST BE SET AT **BUILD** TIME, NOT RUNTIME. `rewrites()` is evaluated
    // once by `next build` and frozen into `.next/routes-manifest.json`; the running server
    // never re-reads it. Setting `API_INTERNAL_URL` only as a compose *runtime* variable looks
    // correct, restarts cleanly, and changes nothing — the manifest still says localhost. That
    // is exactly the trap this comment exists to stop, because it was fallen into first:
    //
    //     docker compose exec frontend-public cat .next/routes-manifest.json
    //
    // is how you check what actually shipped. `frontend/public/Dockerfile` therefore takes
    // `API_INTERNAL_URL` as a build ARG, and `docker-compose.yml` passes it under `build.args`
    // as well as `environment` — the latter is still needed for server-side rendering.
    //
    // The literal below is only the fallback for `npm run dev:public` on a developer's machine,
    // where the API really is on the host at 5080.
    const apiBase = process.env.API_INTERNAL_URL ?? 'http://localhost:5080/api';
    return [{ source: '/api/:path*', destination: `${apiBase}/:path*` }];
  },
};
export default nextConfig;
