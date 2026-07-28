'use client';

// Route-level error boundary (App Router). Prevents blank pages when a
// Server Component throws (e.g. the API is unreachable).
export default function Error({ error, reset }: { error: Error; reset: () => void }) {
  return (
    <main className="mx-auto max-w-[760px] p-6">
      <h1 className="font-display text-2xl font-bold">Something went wrong</h1>
      <p className="mt-2 text-sm text-danger-600">{error.message}</p>
      <button
        onClick={reset}
        className="mt-4 h-10 rounded-md bg-primary-600 px-4 font-semibold text-white hover:bg-primary-700"
      >
        Try again
      </button>
    </main>
  );
}
