// No-login client portal (design system §7). Renders a shortlist of Portal Candidate Cards
// with the one-click Feedback Bar. No agency-internal data leaks here.
export default function PortalPage({ params }: { params: { token: string } }) {
  return (
    <main className="mx-auto max-w-[760px] px-6 py-12">
      <h1 className="font-display text-[32px] font-bold leading-10">Candidates for [Job Title]</h1>
      <p className="mt-2 text-sm text-ink-600">Portal token: <span className="font-mono">{params.token}</span></p>
      {/* TODO: fetch shortlist by token; render Portal Candidate Cards + Feedback Bar. */}
    </main>
  );
}
