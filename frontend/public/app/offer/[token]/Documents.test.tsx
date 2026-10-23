import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { PortalDocument, PortalDocuments, PortalOffer, PortalOfferLink } from '@recruitops/types';
import { OfferPortal } from './OfferPortal';
import { ApiError } from '../../../lib/api';

// Joining documents on the offer link (ADR-0030 §5b), after accepting. The API decides what is ready,
// sent or sent back; these tests pin what the page shows for each answer and what it sends.
const apiMock = vi.hoisted(() => vi.fn());
const uploadMock = vi.hoisted(() => vi.fn());
vi.mock('../../../lib/api', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../../lib/api')>()),
  api: apiMock,
  upload: uploadMock,
}));

const TOKEN = 'tok-abc';
const BASE = `/public/offers/${TOKEN}`;

const link: PortalOfferLink = {
  companyName: 'Yoma Commercial Bank', state: 'Open', maskedEmail: 'k•••••@gmail.com',
  expiresOn: '2026-10-30', contactEmail: 'recruitment@ymb.test',
};

const accepted: PortalOffer = {
  companyName: 'Yoma Commercial Bank', position: 'Senior Credit Analyst', departmentName: 'Credit Risk',
  baseSalary: 480000, allowances: 45000, joiningDate: '2026-11-20', probationMonths: 6, expiresOn: '2026-10-30',
  state: 'Accepted', letterText: 'Dear Ko Aung Kyaw Moe,', letterSha256: 'ab'.repeat(32),
  signedName: 'Aung Kyaw Moe', answeredAt: '2026-10-23T09:00:00Z', contactEmail: 'recruitment@ymb.test', documentsOpen: true,
};

function doc(kind: PortalDocument['kind'], overrides: Partial<PortalDocument> = {}): PortalDocument {
  return {
    kind, required: kind !== 'Photo', status: 'Missing', complete: false, message: null, files: [],
    bank: kind === 'BankAccount' ? { bankName: null, accountName: null, accountNumberMasked: null } : null,
    ...overrides,
  };
}

function documents(overrides: Partial<PortalDocuments> = {}, docs: PortalDocument[] = []): PortalDocuments {
  const all = (['NrcFront', 'NrcBack', 'BankAccount', 'Education', 'Photo'] as const)
    .map((k) => docs.find((d) => d.kind === k) ?? doc(k));
  return {
    openUntil: '2026-11-20', signedAt: '2026-10-23T09:00:00Z', maskedEmail: 'k•••••@gmail.com', documents: all,
    requiredItems: 3, requiredItemsReady: 0, sent: false, canSend: false,
    ...overrides,
  };
}

function serve(routes: Record<string, (init?: RequestInit) => Promise<unknown>>) {
  apiMock.mockImplementation((path: string, init?: RequestInit) => {
    const key = `${init?.method ?? 'GET'} ${path}`;
    const route = routes[key];
    return route ? route(init) : Promise.reject(new Error(`unexpected call: ${key}`));
  });
}

beforeEach(() => {
  apiMock.mockReset();
  uploadMock.mockReset();
  window.scrollTo = vi.fn() as unknown as typeof window.scrollTo;
});

async function open(current: PortalDocuments, more: Record<string, (init?: RequestInit) => Promise<unknown>> = {}) {
  serve({
    [`GET ${BASE}`]: () => Promise.resolve(link),
    [`GET ${BASE}/offer`]: () => Promise.resolve(accepted),
    [`GET ${BASE}/documents`]: () => Promise.resolve(current),
    ...more,
  });
  render(<OfferPortal token={TOKEN} />);
  await screen.findByRole('heading', { name: 'A few documents before day one' });
}

describe('the documents step', () => {
  it('follows an accepted offer while documents can be sent, with what is required and until when', async () => {
    await open(documents());
    expect(screen.getByText(/any time before 20 November 2026/)).toBeInTheDocument();
    const nrc = screen.getByTestId('item-Nrc');
    expect(within(nrc).getByText('Required')).toBeInTheDocument();
    expect(within(nrc).getByLabelText('Front — Choose file or take a photo')).toBeInTheDocument();
    expect(within(screen.getByTestId('item-Photo')).getByText(/Optional — you can do this on day one/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Send for checking' })).toBeDisabled();
    expect(screen.getByText('0 of 3 required items ready')).toBeInTheDocument();
  });

  it('an accepted offer whose documents are closed shows the accepted page instead', async () => {
    serve({
      [`GET ${BASE}`]: () => Promise.resolve(link),
      [`GET ${BASE}/offer`]: () => Promise.resolve({ ...accepted, documentsOpen: false }),
    });
    render(<OfferPortal token={TOKEN} />);
    expect(await screen.findByRole('heading', { name: 'Offer accepted — welcome' })).toBeInTheDocument();
    expect(apiMock).not.toHaveBeenCalledWith(`${BASE}/documents`, expect.anything());
  });

  it('uploads a chosen file to its document and shows its name, never its content', async () => {
    const after = documents({}, [doc('NrcFront', { status: 'Submitted', complete: true, files: [{ id: 'f1', fileName: 'IMG_2041.jpg', sizeBytes: 1_887_436 }] })]);
    uploadMock.mockResolvedValue(after);
    await open(documents());

    const file = new File([new Uint8Array([0xff, 0xd8, 0xff, 0xe0])], 'IMG_2041.jpg', { type: 'image/jpeg' });
    await userEvent.upload(screen.getByLabelText('Front — Choose file or take a photo'), file);

    expect(uploadMock).toHaveBeenCalledWith(`${BASE}/documents/NrcFront/files`, expect.any(FormData));
    expect((uploadMock.mock.calls[0][1] as FormData).get('file')).toBeInstanceOf(File);
    expect(await screen.findByText('IMG_2041.jpg')).toBeInTheDocument();
    expect(screen.getByText(/1\.8 MB/)).toBeInTheDocument();
    expect(screen.getByLabelText('Front — Replace')).toBeInTheDocument();
  });

  it('refuses a file over 10 MB before sending it, and shows the API refusal for a wrong type', async () => {
    uploadMock.mockRejectedValue(new ApiError(409, 'Conflict', 'Upload a photo (JPG or PNG) or a PDF.'));
    await open(documents());
    const huge = new File(['x'], 'huge.jpg', { type: 'image/jpeg' });
    Object.defineProperty(huge, 'size', { value: 10 * 1024 * 1024 + 1 });
    await userEvent.upload(screen.getByLabelText('Front — Choose file or take a photo'), huge);
    expect(await screen.findByRole('alert')).toHaveTextContent('over 10 MB');
    expect(uploadMock).not.toHaveBeenCalled();

    await userEvent.upload(screen.getByLabelText('Back — Choose file or take a photo'), new File(['%PDF'], 'nrc.jpg', { type: 'image/jpeg' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('Upload a photo (JPG or PNG) or a PDF.');
  });

  it('saves typed bank details and shows the number back masked, never whole', async () => {
    let body: unknown;
    const saved = documents({}, [doc('BankAccount', {
      status: 'Submitted', bank: { bankName: 'KBZ Bank', accountName: 'AUNG KYAW MOE', accountNumberMasked: '•••• 4471' },
    })]);
    await open(documents(), {
      [`PUT ${BASE}/documents/bank`]: (init) => { body = JSON.parse(String(init?.body)); return Promise.resolve(saved); },
    });
    await userEvent.type(screen.getByLabelText('Bank'), 'KBZ Bank');
    await userEvent.type(screen.getByLabelText('Name on the account'), 'AUNG KYAW MOE');
    await userEvent.type(screen.getByLabelText('Account number'), '0012 3456 7890 4471');
    await userEvent.click(screen.getByRole('button', { name: 'Save bank details' }));

    expect(body).toEqual({ bankName: 'KBZ Bank', accountName: 'AUNG KYAW MOE', accountNumber: '0012 3456 7890 4471' });
    expect(await screen.findByText('•••• 4471')).toBeInTheDocument();
    expect(screen.getByLabelText('Account number')).toHaveValue('');
    expect(document.body.textContent).not.toContain('0012 3456 7890 4471');
  });

  it('shows a sent-back document with the recruiter’s own words, and a checked one as done', async () => {
    await open(documents({}, [
      doc('Education', {
        status: 'Returned', complete: true, message: 'ဓာတ်ပုံ မှုန်ဝါးနေပါသည်။\nThe photo is blurred — please retake it.',
        files: [{ id: 'e1', fileName: 'degree.jpg', sizeBytes: 2_400_000 }],
      }),
      doc('Photo', { status: 'Approved', complete: true, files: [{ id: 'p1', fileName: 'me.png', sizeBytes: 90_000 }] }),
    ]));
    const education = screen.getByTestId('item-Education');
    expect(within(education).getByText('Please upload this again')).toBeInTheDocument();
    expect(within(education).getByText(/The photo is blurred/)).toBeInTheDocument();
    expect(within(education).getByRole('button', { name: 'Remove degree.jpg' })).toBeInTheDocument();
    const photo = screen.getByTestId('item-Photo');
    expect(within(photo).getByText('Checked — thank you')).toBeInTheDocument();
    // Approved is final: nothing to change.
    expect(within(photo).queryByText('Replace')).not.toBeInTheDocument();
  });

  it('sends for checking, then shows what is being checked', async () => {
    const ready = documents({ canSend: true, requiredItemsReady: 3 }, [
      doc('NrcFront', { status: 'Approved', complete: true, files: [{ id: 'a', fileName: 'a.jpg', sizeBytes: 1000 }] }),
      doc('NrcBack', { status: 'Approved', complete: true, files: [{ id: 'b', fileName: 'b.jpg', sizeBytes: 1000 }] }),
      doc('BankAccount', { status: 'Submitted', complete: true, files: [{ id: 'c', fileName: 'c.jpg', sizeBytes: 1000 }] }),
      doc('Education', { status: 'Submitted', complete: true, files: [{ id: 'd', fileName: 'd.pdf', sizeBytes: 1000 }] }),
    ]);
    await open(ready, { [`POST ${BASE}/documents/send`]: () => Promise.resolve({ ...ready, sent: true, canSend: false }) });
    await userEvent.click(screen.getByRole('button', { name: 'Send for checking' }));

    expect(await screen.findByRole('heading', { name: 'Sent for checking — thank you' })).toBeInTheDocument();
    const rows = screen.getAllByRole('listitem').map((li) => li.textContent);
    expect(rows).toEqual([
      'NRC — front and backChecked',
      'Bank account detailsBeing checked',
      'Education certificatesBeing checked',
      'Photo for your staff cardOptional · not added',
    ]);
    await userEvent.click(screen.getByRole('button', { name: 'Change a document' }));
    expect(await screen.findByRole('heading', { name: 'A few documents before day one' })).toBeInTheDocument();
  });

  it('a session that ended mid-upload goes back to asking for a code', async () => {
    uploadMock.mockRejectedValue(new ApiError(401, 'Unauthorized', null));
    await open(documents());
    await userEvent.upload(screen.getByLabelText('Front — Choose file or take a photo'), new File(['x'], 'a.jpg', { type: 'image/jpeg' }));
    await waitFor(() => expect(screen.getByRole('heading', { name: 'You have an offer from Yoma Commercial Bank' })).toBeInTheDocument());
    expect(screen.getByText(/the page locked itself/)).toBeInTheDocument();
  });
});
