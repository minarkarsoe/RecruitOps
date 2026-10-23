import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { PortalOffer, PortalOfferLink } from '@recruitops/types';
import { OfferPortal } from './OfferPortal';
import { ApiError } from '../../../lib/api';

// The candidate's offer link (ADR-0030 §5a). The API decides everything; these tests pin what the page
// shows for each answer — and that it never shows more than the API gave it.
const apiMock = vi.hoisted(() => vi.fn());
vi.mock('../../../lib/api', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../../lib/api')>()),
  api: apiMock,
}));

const TOKEN = 'tok-abc';
const BASE = `/public/offers/${TOKEN}`;

const link: PortalOfferLink = {
  companyName: 'Yoma Commercial Bank', state: 'Open', maskedEmail: 'k•••••@gmail.com',
  expiresOn: '2026-10-30', contactEmail: 'recruitment@ymb.test',
};

function offer(overrides: Partial<PortalOffer> = {}): PortalOffer {
  return {
    companyName: 'Yoma Commercial Bank', position: 'Senior Credit Analyst', departmentName: 'Credit Risk',
    baseSalary: 480000, allowances: 45000, joiningDate: '2026-11-20', probationMonths: 6, expiresOn: '2026-10-30',
    state: 'Open', letterText: 'Dear Ko Aung Kyaw Moe,\n\nWe are pleased to offer you…', letterSha256: 'ab'.repeat(32),
    signedName: null, answeredAt: null, contactEmail: 'recruitment@ymb.test',
    ...overrides,
  };
}

const unauthorized = () => new ApiError(401, 'Unauthorized', null);
const refused = (detail: string) => new ApiError(409, 'Conflict', detail);

/** Routes `api()` by method and path; anything unlisted fails the test loudly. */
function serve(routes: Record<string, () => Promise<unknown>>) {
  apiMock.mockImplementation((path: string, init?: RequestInit) => {
    const key = `${init?.method ?? 'GET'} ${path}`;
    const route = routes[key];
    return route ? route() : Promise.reject(new Error(`unexpected call: ${key}`));
  });
}

function sent(method: string, path: string): unknown {
  const call = apiMock.mock.calls.find(([p, init]) => p === path && (init as RequestInit | undefined)?.method === method);
  return call ? JSON.parse(String((call[1] as RequestInit).body)) : undefined;
}

beforeEach(() => {
  apiMock.mockReset();
  // Each step scrolls to the top, as a new page would; jsdom has no layout to scroll.
  window.scrollTo = vi.fn() as unknown as typeof window.scrollTo;
});

describe('what the link alone shows', () => {
  it('one page for an unknown, mistyped or withdrawn link', async () => {
    serve({ [`GET ${BASE}`]: () => Promise.reject(new ApiError(404, 'Not Found', null)) });
    render(<OfferPortal token={TOKEN} />);
    expect(await screen.findByRole('heading', { name: "This link doesn't work" })).toBeInTheDocument();
  });

  it('an expired offer says until when, and who to contact', async () => {
    serve({ [`GET ${BASE}`]: () => Promise.resolve({ ...link, state: 'Expired' }) });
    render(<OfferPortal token={TOKEN} />);
    expect(await screen.findByRole('heading', { name: 'This offer has expired' })).toBeInTheDocument();
    expect(screen.getByText(/30 October 2026/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'recruitment@ymb.test' })).toHaveAttribute('href', 'mailto:recruitment@ymb.test');
  });

  it('only the company and the masked address until a code is entered', async () => {
    serve({
      [`GET ${BASE}`]: () => Promise.resolve(link),
      [`GET ${BASE}/offer`]: () => Promise.reject(unauthorized()),
    });
    render(<OfferPortal token={TOKEN} />);
    expect(await screen.findByRole('heading', { name: 'You have an offer from Yoma Commercial Bank' })).toBeInTheDocument();
    expect(screen.getByText('k•••••@gmail.com')).toBeInTheDocument();
    expect(screen.queryByText(/Senior Credit Analyst/)).not.toBeInTheDocument();
    expect(screen.queryByText(/480,000/)).not.toBeInTheDocument();
  });

  it('opens straight to the offer while an earlier session is still live', async () => {
    serve({
      [`GET ${BASE}`]: () => Promise.resolve(link),
      [`GET ${BASE}/offer`]: () => Promise.resolve(offer()),
    });
    render(<OfferPortal token={TOKEN} />);
    expect(await screen.findByRole('heading', { name: 'Senior Credit Analyst' })).toBeInTheDocument();
  });
});

describe('the code', () => {
  it('sends a code, shows the API refusal as written, then opens the offer', async () => {
    let offerCalls = 0;
    serve({
      [`GET ${BASE}`]: () => Promise.resolve(link),
      [`GET ${BASE}/offer`]: () => (offerCalls++ === 0 ? Promise.reject(unauthorized()) : Promise.resolve(offer())),
      [`POST ${BASE}/code`]: () => Promise.resolve(undefined),
      [`POST ${BASE}/verify`]: () => Promise.reject(refused("That code isn't right. 4 tries left.")),
    });
    render(<OfferPortal token={TOKEN} />);

    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: 'Email me a code' }));
    const field = await screen.findByLabelText('6-digit code');
    expect(field).toHaveAttribute('autocomplete', 'one-time-code');
    expect(field).toHaveAttribute('inputmode', 'numeric');

    await user.type(field, '111111');
    await user.click(screen.getByRole('button', { name: 'Continue' }));
    expect(await screen.findByRole('alert')).toHaveTextContent("That code isn't right. 4 tries left.");
    expect(sent('POST', `${BASE}/verify`)).toEqual({ code: '111111' });

    serve({
      [`POST ${BASE}/verify`]: () => Promise.resolve(undefined),
      [`GET ${BASE}/offer`]: () => Promise.resolve(offer()),
    });
    await user.clear(field);
    await user.type(field, '482913');
    await user.click(screen.getByRole('button', { name: 'Continue' }));
    expect(await screen.findByRole('heading', { name: 'Senior Credit Analyst' })).toBeInTheDocument();
    expect(screen.getByText('MMK 480,000')).toBeInTheDocument();
    expect(screen.getByText('6 months')).toBeInTheDocument();
  });

  it('never shows the server text of a failure the API did not write for the candidate', async () => {
    serve({
      [`GET ${BASE}`]: () => Promise.resolve(link),
      [`GET ${BASE}/offer`]: () => Promise.reject(unauthorized()),
      [`POST ${BASE}/code`]: () => Promise.reject(new ApiError(500, 'Internal Server Error', 'Sequence contains no elements')),
    });
    render(<OfferPortal token={TOKEN} />);
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: 'Email me a code' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('We could not send a code just now.');
    expect(screen.queryByText(/Sequence contains/)).not.toBeInTheDocument();
  });
});

describe('answering', () => {
  async function openOffer(extra: Record<string, () => Promise<unknown>> = {}) {
    serve({
      [`GET ${BASE}`]: () => Promise.resolve(link),
      [`GET ${BASE}/offer`]: () => Promise.resolve(offer()),
      ...extra,
    });
    render(<OfferPortal token={TOKEN} />);
    await screen.findByRole('heading', { name: 'Senior Credit Analyst' });
    return userEvent.setup();
  }

  it('signs with the typed name, the consent and the hash of the letter shown', async () => {
    const user = await openOffer({
      [`POST ${BASE}/accept`]: () => Promise.resolve(offer({ state: 'Accepted', signedName: 'Aung Kyaw Moe', answeredAt: '2026-10-23T09:00:00Z' })),
    });
    await user.click(screen.getByRole('button', { name: 'Accept & sign' }));

    const submit = screen.getByRole('button', { name: 'Accept & sign' });
    expect(submit).toBeDisabled();
    await user.type(screen.getByLabelText('Your full name'), 'Aung Kyaw Moe');
    expect(submit).toBeDisabled(); // not without the consent
    await user.click(screen.getByRole('checkbox', { name: /I have read the offer letter/ }));
    await user.click(submit);

    expect(await screen.findByRole('heading', { name: 'Offer accepted — welcome' })).toBeInTheDocument();
    expect(screen.getByText(/Signed by Aung Kyaw Moe on/)).toBeInTheDocument();
    expect(sent('POST', `${BASE}/accept`)).toEqual({ typedName: 'Aung Kyaw Moe', consent: true, letterSha256: 'ab'.repeat(32) });
  });

  it('goes back to the code when the session has ended, saying why', async () => {
    const user = await openOffer({ [`POST ${BASE}/accept`]: () => Promise.reject(unauthorized()) });
    await user.click(screen.getByRole('button', { name: 'Accept & sign' }));
    await user.type(screen.getByLabelText('Your full name'), 'Aung Kyaw Moe');
    await user.click(screen.getByRole('checkbox', { name: /I have read the offer letter/ }));
    await user.click(screen.getByRole('button', { name: 'Accept & sign' }));

    expect(await screen.findByRole('heading', { name: 'You have an offer from Yoma Commercial Bank' })).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('locked itself');
  });

  it('reloads the letter when it changed since it was read, rather than signing blind', async () => {
    const user = await openOffer({
      [`POST ${BASE}/accept`]: () => Promise.reject(refused('The offer letter changed since you opened it. Reload the page and read it again before signing.')),
    });
    await user.click(screen.getByRole('button', { name: 'Accept & sign' }));
    await user.type(screen.getByLabelText('Your full name'), 'Aung Kyaw Moe');
    await user.click(screen.getByRole('checkbox', { name: /I have read the offer letter/ }));
    await user.click(screen.getByRole('button', { name: 'Accept & sign' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('changed since you opened it');
    await waitFor(() => expect(apiMock.mock.calls.filter(([p, init]) => p === `${BASE}/offer` && !(init as RequestInit | undefined)?.method)).toHaveLength(2));
  });

  it('declines with the reason given, or none', async () => {
    const user = await openOffer({
      [`POST ${BASE}/decline`]: () => Promise.resolve(offer({ state: 'Declined', answeredAt: '2026-10-23T09:00:00Z' })),
    });
    await user.click(screen.getByRole('button', { name: 'Decline this offer' }));
    await user.type(screen.getByLabelText(/Anything you'd like to tell us/), '  Accepted another offer  ');
    await user.click(screen.getByRole('button', { name: 'Decline the offer' }));

    expect(await screen.findByRole('heading', { name: 'You declined this offer' })).toBeInTheDocument();
    expect(sent('POST', `${BASE}/decline`)).toEqual({ reason: 'Accepted another offer' });
  });

  it('shows the letter as text — a name is never markup', async () => {
    serve({
      [`GET ${BASE}`]: () => Promise.resolve(link),
      [`GET ${BASE}/offer`]: () => Promise.resolve(offer({ letterText: 'Dear <img src=x onerror=alert(1)>,\n\nWelcome.' })),
    });
    const { container } = render(<OfferPortal token={TOKEN} />);
    const user = userEvent.setup();
    await user.click(await screen.findByRole('button', { name: /Read the full offer letter/ }));

    expect(screen.getByTestId('letter')).toHaveTextContent('Dear <img src=x onerror=alert(1)>,');
    expect(container.querySelector('img')).toBeNull();
    expect(screen.getByRole('button', { name: 'Print or save as PDF' })).toBeInTheDocument();
  });
});
