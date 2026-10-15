import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ApplicationFormField } from '@recruitops/types';
import { ApplicationForm } from './ApplicationForm';

// The form talks to the API through `lib/api`, which is covered by its own tests. Mocking it
// here keeps these tests about the form's behaviour rather than about fetch.
const apiMock = vi.hoisted(() => vi.fn());
vi.mock('../../../lib/api', () => ({ api: apiMock }));

const TOKEN = 'tok_abc123';

function renderForm(fields?: ApplicationFormField[]) {
  return render(
    <ApplicationForm
      token={TOKEN}
      formFieldsJson={fields ? JSON.stringify(fields) : null}
    />,
  );
}

/** The body the form POSTed, parsed. */
function sentPayload() {
  const init = apiMock.mock.calls[0][1] as RequestInit;
  return JSON.parse(String(init.body));
}

beforeEach(() => {
  apiMock.mockReset();
  apiMock.mockResolvedValue({ message: 'Thank you. Your application has been received.' });
});

describe('the contact rule', () => {
  // Mirrors a server rule. Checked client-side too so the applicant finds out BEFORE losing
  // everything they typed — this form is filled once, by a stranger, usually on a phone.
  it('refuses to submit with neither email nor phone, and does not call the API', async () => {
    const user = userEvent.setup();
    renderForm();

    await user.type(screen.getByLabelText(/full name/i), 'Daw Hnin Ei Khaing');
    await user.click(screen.getByRole('button', { name: /submit application/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent(
      /either an email address or a phone number/i,
    );
    expect(apiMock).not.toHaveBeenCalled();
  });

  it('accepts email alone', async () => {
    const user = userEvent.setup();
    renderForm();

    await user.type(screen.getByLabelText(/full name/i), 'U Thura Win');
    await user.type(screen.getByLabelText(/^email$/i), 'thura@example.com');
    await user.click(screen.getByRole('button', { name: /submit application/i }));

    await waitFor(() => expect(apiMock).toHaveBeenCalledTimes(1));
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('accepts phone alone', async () => {
    const user = userEvent.setup();
    renderForm();

    await user.type(screen.getByLabelText(/full name/i), 'Ma Thiri Kyaw');
    await user.type(screen.getByLabelText(/^phone$/i), '09-771234567');
    await user.click(screen.getByRole('button', { name: /submit application/i }));

    await waitFor(() => expect(apiMock).toHaveBeenCalledTimes(1));
  });

  it('treats whitespace as no contact detail at all', async () => {
    const user = userEvent.setup();
    renderForm();

    await user.type(screen.getByLabelText(/full name/i), 'Ko Zaw Min Htun');
    await user.type(screen.getByLabelText(/^phone$/i), '   ');
    await user.click(screen.getByRole('button', { name: /submit application/i }));

    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(apiMock).not.toHaveBeenCalled();
  });
});

describe('what gets sent', () => {
  it('posts to the token-scoped apply endpoint, URL-encoded', async () => {
    const user = userEvent.setup();
    render(<ApplicationForm token="tok/with space" formFieldsJson={null} />);

    await user.type(screen.getByLabelText(/full name/i), 'A');
    await user.type(screen.getByLabelText(/^email$/i), 'a@example.com');
    await user.click(screen.getByRole('button', { name: /submit application/i }));

    await waitFor(() => expect(apiMock).toHaveBeenCalled());
    expect(apiMock.mock.calls[0][0]).toBe('/public/jobs/tok%2Fwith%20space/apply');
    expect((apiMock.mock.calls[0][1] as RequestInit).method).toBe('POST');
  });

  it('sends customFieldsJson as null when the posting defines no questions', async () => {
    const user = userEvent.setup();
    renderForm();

    await user.type(screen.getByLabelText(/full name/i), 'A');
    await user.type(screen.getByLabelText(/^email$/i), 'a@example.com');
    await user.click(screen.getByRole('button', { name: /submit application/i }));

    await waitFor(() => expect(apiMock).toHaveBeenCalled());
    // `null`, not `"{}"` — an empty object would claim the applicant answered a schema that
    // does not exist.
    expect(sentPayload().customFieldsJson).toBeNull();
  });

  it('sends the answers as a JSON object keyed by field key when questions exist', async () => {
    const user = userEvent.setup();
    renderForm([
      { key: 'years', label: 'Years of experience', type: 'number', required: true },
      { key: 'why', label: 'Why this role?', type: 'textarea', required: false },
    ]);

    await user.type(screen.getByLabelText(/full name/i), 'A');
    await user.type(screen.getByLabelText(/^email$/i), 'a@example.com');
    await user.type(screen.getByLabelText(/years of experience/i), '8');
    await user.type(screen.getByLabelText(/why this role/i), 'Credit risk background.');
    await user.click(screen.getByRole('button', { name: /submit application/i }));

    await waitFor(() => expect(apiMock).toHaveBeenCalled());
    // Values stay strings on the wire; the server coerces and is the authority on what each
    // type accepts. Coercing here too would give two places to disagree about "8".
    expect(JSON.parse(sentPayload().customFieldsJson)).toEqual({
      years: '8',
      why: 'Credit risk background.',
    });
  });
});

describe('failure is not a leak', () => {
  it('shows a generic message and never echoes the API error to a stranger', async () => {
    const user = userEvent.setup();
    // A real server message: internal, not written for the public.
    apiMock.mockRejectedValue(new Error('API 500: Internal Server Error at CandidateService:88'));
    renderForm();

    await user.type(screen.getByLabelText(/full name/i), 'A');
    await user.type(screen.getByLabelText(/^email$/i), 'a@example.com');
    await user.click(screen.getByRole('button', { name: /submit application/i }));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent(/something went wrong sending your application/i);

    // The specific guarantee: none of the server's wording reaches the page.
    const page = document.body.textContent ?? '';
    expect(page).not.toMatch(/500/);
    expect(page).not.toMatch(/Internal Server Error/i);
    expect(page).not.toMatch(/CandidateService/);
  });

  it('leaves the form filled in so a retry does not mean retyping', async () => {
    const user = userEvent.setup();
    apiMock.mockRejectedValue(new Error('API 503: Service Unavailable'));
    renderForm();

    await user.type(screen.getByLabelText(/full name/i), 'Daw Moe Moe Aung');
    await user.type(screen.getByLabelText(/^email$/i), 'moe@example.com');
    await user.click(screen.getByRole('button', { name: /submit application/i }));

    await screen.findByRole('alert');
    expect(screen.getByLabelText(/full name/i)).toHaveValue('Daw Moe Moe Aung');
    expect(screen.getByRole('button', { name: /submit application/i })).toBeEnabled();
  });
});

describe('success', () => {
  it('replaces the form with the server’s message, so it cannot be submitted twice', async () => {
    const user = userEvent.setup();
    apiMock.mockResolvedValue({ message: 'Thank you. We will be in touch.' });
    renderForm();

    await user.type(screen.getByLabelText(/full name/i), 'A');
    await user.type(screen.getByLabelText(/^email$/i), 'a@example.com');
    await user.click(screen.getByRole('button', { name: /submit application/i }));

    expect(await screen.findByText(/application received/i)).toBeInTheDocument();
    expect(screen.getByText('Thank you. We will be in touch.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /submit application/i })).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/full name/i)).not.toBeInTheDocument();
  });
});

describe('customer-defined questions', () => {
  it('renders each field type with the right control', () => {
    renderForm([
      { key: 'a', label: 'Plain text', type: 'text', required: false },
      { key: 'b', label: 'Long answer', type: 'textarea', required: false },
      { key: 'c', label: 'How many', type: 'number', required: false },
      { key: 'd', label: 'Available from', type: 'date', required: false },
      { key: 'e', label: 'Preferred site', type: 'select', required: false, options: ['Yangon', 'Mandalay'] },
      { key: 'f', label: 'I agree', type: 'checkbox', required: false },
    ]);

    expect(screen.getByLabelText(/plain text/i)).toHaveProperty('type', 'text');
    expect(screen.getByLabelText(/long answer/i).tagName).toBe('TEXTAREA');
    expect(screen.getByLabelText(/how many/i)).toHaveProperty('type', 'number');
    expect(screen.getByLabelText(/available from/i)).toHaveProperty('type', 'date');
    expect(screen.getByLabelText(/preferred site/i).tagName).toBe('SELECT');
    expect(screen.getByLabelText(/i agree/i)).toHaveProperty('type', 'checkbox');
  });

  it('gives a select an empty first option, so it does not silently default to the first answer', () => {
    renderForm([
      { key: 'site', label: 'Preferred site', type: 'select', required: true, options: ['Yangon', 'Mandalay'] },
    ]);

    const select = screen.getByLabelText(/preferred site/i) as HTMLSelectElement;
    expect(select.value).toBe('');
    expect(select.options[0].value).toBe('');
  });

  it('mirrors `required` onto the control so the browser catches it before a round trip', () => {
    renderForm([
      { key: 'must', label: 'Required question', type: 'text', required: true },
      { key: 'may', label: 'Optional question', type: 'text', required: false },
    ]);

    expect(screen.getByLabelText(/required question/i)).toBeRequired();
    expect(screen.getByLabelText(/optional question/i)).not.toBeRequired();
  });

  it('sends a checkbox as the string "true"/"false", matching how the server reads it', async () => {
    const user = userEvent.setup();
    renderForm([{ key: 'agree', label: 'I agree', type: 'checkbox', required: false }]);

    await user.type(screen.getByLabelText(/full name/i), 'A');
    await user.type(screen.getByLabelText(/^email$/i), 'a@example.com');
    await user.click(screen.getByLabelText(/i agree/i));
    await user.click(screen.getByRole('button', { name: /submit application/i }));

    await waitFor(() => expect(apiMock).toHaveBeenCalled());
    expect(JSON.parse(sentPayload().customFieldsJson)).toEqual({ agree: 'true' });
  });

  it('renders nothing extra when the schema is malformed, rather than blanking the page', () => {
    // `parseFormFields` swallows bad JSON by contract — the form must still be usable.
    render(<ApplicationForm token={TOKEN} formFieldsJson={'{ not json'} />);

    expect(screen.getByLabelText(/full name/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /submit application/i })).toBeInTheDocument();
  });
});
