/** @type {import('next').NextConfig} */
const nextConfig = {
  // Lets the shared workspace packages be transpiled by Next.
  transpilePackages: ['@recruitops/ui', '@recruitops/types'],
  async rewrites() {
    // Browser-side calls go through here in development.
    return [{ source: '/api/:path*', destination: 'http://localhost:5080/api/:path*' }];
  },
};
export default nextConfig;
