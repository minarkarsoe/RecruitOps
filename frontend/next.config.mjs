/** @type {import('next').NextConfig} */
const nextConfig = {
  async rewrites() {
    // Proxy API calls to the .NET backend in development.
    return [{ source: '/api/:path*', destination: 'http://localhost:5080/api/:path*' }];
  },
};
export default nextConfig;
