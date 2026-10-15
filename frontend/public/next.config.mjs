/** @type {import('next').NextConfig} */
const nextConfig = {
  // Lets the shared workspace packages be transpiled by Next.
  transpilePackages: ['@recruitops/ui', '@recruitops/types'],
  async rewrites() {
    // Browser-side calls go through here in development.
    return [{ source: '/api/:path*', destination: 'http://localhost:5080/api/:path*' }];
  },
  // Public job pages and the application form. No script CSP yet: Next's inline hydration
  // scripts need a per-request nonce (middleware), which is its own piece of work. These carry no
  // breakage risk. `frame-ancestors 'self'` stops another site framing the application form; a
  // customer who wants to embed it in their careers site adds that origin here.
  async headers() {
    return [{
      source: '/:path*',
      headers: [
        { key: 'Content-Security-Policy', value: "frame-ancestors 'self'; object-src 'none'; base-uri 'self'" },
        { key: 'X-Content-Type-Options', value: 'nosniff' },
        { key: 'Referrer-Policy', value: 'strict-origin-when-cross-origin' },
        { key: 'Permissions-Policy', value: 'camera=(), microphone=(), geolocation=()' },
      ],
    }];
  },
};
export default nextConfig;
