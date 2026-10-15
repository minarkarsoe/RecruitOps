'use client';

// Route-level error boundary (App Router). Prevents blank pages when a
// Server Component throws (e.g. the API is unreachable).
//
// ⚠️ `error.message` is rendered to a stranger on a public page. That is deliberate here — this
// boundary only catches our own thrown errors, and `lib/api` does not put server detail into
// them (see ApplicationForm's catch, which discards the API's message for the same reason). If
// a future error carries anything from the API, this line has to go.
export default function Error({ error, reset }: { error: Error; reset: () => void }) {
  return (
    <main className="mx-auto max-w-[560px] px-5 py-10">
      <h1 className="text-2xl font-bold tracking-tight">Something went wrong</h1>
      <div role="alert" className="mt-4 rounded-md border border-critical-100 bg-critical-50 px-3.5 py-3">
        <p className="text-base text-critical-700">{error.message}</p>
      </div>
      <button
        onClick={reset}
        className="mt-5 h-12 rounded-md bg-brand-700 px-5 text-md font-medium text-white
          transition-colors hover:bg-brand-800"
      >
        Try again
      </button>
    </main>
  );
}
