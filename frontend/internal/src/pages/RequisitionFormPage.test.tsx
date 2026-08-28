import { describe, expect, it, vi, beforeEach } from 'vitest';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { RequisitionFormPage } from './RequisitionFormPage';

/*
 * One component serves both create and edit, deliberately — they are the same form against the
 * same fields, and duplicating it would guarantee the two drift the first time a field is
 * added. That sharing is the thing worth testing: the only differences are where the initial
 * values come from and which verb is sent, so a regression shows up as one mode quietly
 * behaving like the other.
 */

const { apiMock } = vi.hoisted(() => ({ apiMock: vi.fn() }));

vi.mock('../lib/api', async () => {
  const actual = await vi.importActual<typeof import('../lib/api')>('../lib/api');
  return { ...actual, api: apiMock };
});

const DEPARTMENTS = [
  { id: 'dep-credit', name: 'Credit Risk' },
  { id: 'dep-retail', name: 'Retail Ops' },
];

const TEMPLATES = [
  { id: 'tpl-analyst', title: 'Credit Analyst', content: 'Assess credit applications.' },
];

function draft(overrides: Record<string, unknown> = {}) {
  return {
    id: 'req-1',
    departmentId: 'dep-credit',
    title: 'Senior Credit Analyst',
    jobDescription: 'Own the credit book.',
    headcount: 3,
    salaryBudget: 2_500_000,
    status: 'Draft',
    ...overrides,
  };
}

/** Routes `GET` calls by path; leaves mutations to the test. */
function serveLookups(extra?: (path: string) => unknown) {
  apiMock.mockImplementation(async (path: string, init?: RequestInit) => {
    if (!init) {
      if (path === '/departments') return DEPARTMENTS;
      if (path === '/jdtemplates') return TEMPLATES;
      if (path.startsWith('/requisitions/')) return draft();
    }
    return extra?.(path) ?? draft();
  });
}

function renderCreate() {
  return render(
    <MemoryRouter initialEntries={['/requisitions/new']}>
      <Routes>
        <Route path="/requisitions/new" element={<RequisitionFormPage mode="create" />} />
        <Route path="/requisitions/:id" element={<p>requisition detail</p>} />
      </Routes>
    </MemoryRouter>,
  );
}

function renderEdit() {
  return render(
    <MemoryRouter initialEntries={['/requisitions/req-1/edit']}>
      <Routes>
        <Route path="/requisitions/:id/edit" element={<RequisitionFormPage mode="edit" />} />
        <Route path="/requisitions/:id" element={<p>requisition detail</p>} />
      </Routes>
    </MemoryRouter>,
  );
}

/** The request body sent by the first mutating call. */
function sentBody() {
  const call = apiMock.mock.calls.find((c) => c[1] !== undefined);
  return JSON.parse(String((call![1] as RequestInit).body));
}

beforeEach(() => {
  apiMock.mockReset();
  serveLookups();
});

describe('create', () => {
  it('POSTs to /requisitions and goes to the new requisition', async () => {
    const user = userEvent.setup();
    renderCreate();

    await screen.findByRole('option', { name: 'Credit Risk' });
    await user.selectOptions(screen.getByLabelText(/department/i), 'dep-credit');
    await user.type(screen.getByLabelText(/position title/i), 'Treasury Analyst');
    await user.type(screen.getByLabelText(/job description/i), 'Manage liquidity.');
    await user.click(screen.getByRole('button', { name: /create draft/i }));

    await waitFor(() =>
      expect(apiMock).toHaveBeenCalledWith('/requisitions', expect.objectContaining({ method: 'POST' })),
    );
    expect(await screen.findByText('requisition detail')).toBeInTheDocument();
  });

  it('does not fetch a requisition — there is nothing to load yet', async () => {
    renderCreate();
    await screen.findByRole('option', { name: 'Credit Risk' });

    expect(apiMock).toHaveBeenCalledWith('/departments');
    expect(apiMock).not.toHaveBeenCalledWith(expect.stringMatching(/^\/requisitions\/./));
  });

  it('defaults headcount to 1 and salary budget to null, not 0', async () => {
    const user = userEvent.setup();
    renderCreate();

    await screen.findByRole('option', { name: 'Credit Risk' });
    await user.selectOptions(screen.getByLabelText(/department/i), 'dep-credit');
    await user.type(screen.getByLabelText(/position title/i), 'X');
    await user.type(screen.getByLabelText(/job description/i), 'Y');
    await user.click(screen.getByRole('button', { name: /create draft/i }));

    await waitFor(() => expect(apiMock).toHaveBeenCalledWith('/requisitions', expect.anything()));
    // `0` would be a stated budget of nothing; `null` is "not stated", and the threshold
    // rule reads the two differently.
    expect(sentBody()).toMatchObject({ headcount: 1, salaryBudget: null });
  });
});

describe('edit', () => {
  it('loads the requisition, prefills every field, and PUTs to its own id', async () => {
    const user = userEvent.setup();
    renderEdit();

    expect(await screen.findByDisplayValue('Senior Credit Analyst')).toBeInTheDocument();
    expect(screen.getByLabelText(/headcount/i)).toHaveValue(3);
    expect(screen.getByLabelText(/salary budget/i)).toHaveValue(2_500_000);
    expect(screen.getByLabelText(/job description/i)).toHaveValue('Own the credit book.');
    expect(screen.getByLabelText(/department/i)).toHaveValue('dep-credit');

    await user.click(screen.getByRole('button', { name: /save changes/i }));

    await waitFor(() =>
      expect(apiMock).toHaveBeenCalledWith('/requisitions/req-1', expect.objectContaining({ method: 'PUT' })),
    );
  });

  it('shows a loading state until the requisition arrives, rather than an empty form', () => {
    apiMock.mockImplementation(() => new Promise(() => {})); // never resolves
    renderEdit();

    expect(screen.getByText(/loading/i)).toBeInTheDocument();
    expect(screen.queryByLabelText(/position title/i)).not.toBeInTheDocument();
  });

  it('surfaces a load failure instead of rendering a form over nothing', async () => {
    apiMock.mockImplementation(async (path: string) => {
      if (path === '/departments') return DEPARTMENTS;
      if (path === '/jdtemplates') return TEMPLATES;
      throw new Error('API 404: Not Found');
    });

    renderEdit();

    expect(await screen.findByRole('alert')).toHaveTextContent(/404/);
  });
});

describe('a requisition that is no longer a Draft', () => {
  // Editing is only offered on a Draft — the backend returns 409 otherwise, because once a
  // requisition is submitted the approvers are deciding on its contents.
  it('says so', async () => {
    apiMock.mockImplementation(async (path: string) => {
      if (path === '/departments') return DEPARTMENTS;
      if (path === '/jdtemplates') return TEMPLATES;
      return draft({ status: 'PendingApproval' });
    });

    renderEdit();

    expect(await screen.findByRole('alert')).toHaveTextContent(/PendingApproval and can no longer be edited/i);
  });

  it('⚠️ but still renders a fully editable, submittable form — the comment says "bounce", the code does not', async () => {
    // `RequisitionFormPage.tsx:49` reads "Bounce rather than let someone fill in a form the API
    // will reject with 409". It sets an error string and nothing else: there is no redirect and
    // no disabling, so the form below stays live. Someone can retype a job description on a
    // requisition the approvers are already deciding on, press Save, and get the 409 the comment
    // says they are being spared.
    //
    // Pinned as-is rather than fixed: whether it should redirect, disable the fields, or hide
    // the form is a product decision about what a Hiring Manager should see here.
    const user = userEvent.setup();
    apiMock.mockImplementation(async (path: string, init?: RequestInit) => {
      if (path === '/departments') return DEPARTMENTS;
      if (path === '/jdtemplates') return TEMPLATES;
      if (init?.method === 'PUT') throw new Error('API 409: Conflict');
      return draft({ status: 'PendingApproval' });
    });

    renderEdit();
    await screen.findByRole('alert');

    const title = screen.getByLabelText(/position title/i);
    expect(title).toBeEnabled();
    await user.clear(title);
    await user.type(title, 'Rewritten after submission');

    const save = screen.getByRole('button', { name: /save changes/i });
    expect(save).toBeEnabled();
    await user.click(save);

    // The request really is sent, and the 409 the comment promised to avoid comes back.
    await waitFor(() =>
      expect(apiMock).toHaveBeenCalledWith('/requisitions/req-1', expect.objectContaining({ method: 'PUT' })),
    );
    expect(await screen.findByRole('alert')).toHaveTextContent(/409/);
  });
});

describe('JD templates', () => {
  it('replaces the job description but keeps a title the user already typed', async () => {
    const user = userEvent.setup();
    renderCreate();

    await screen.findByRole('option', { name: 'Credit Analyst' });
    await user.type(screen.getByLabelText(/position title/i), 'My own title');
    await user.selectOptions(screen.getByLabelText(/start from a jd template/i), 'tpl-analyst');

    // `f.title || t.title` — the template fills a blank title but never overwrites one.
    expect(screen.getByLabelText(/position title/i)).toHaveValue('My own title');
    expect(screen.getByLabelText(/job description/i)).toHaveValue('Assess credit applications.');
  });

  it('fills a blank title from the template', async () => {
    const user = userEvent.setup();
    renderCreate();

    await screen.findByRole('option', { name: 'Credit Analyst' });
    await user.selectOptions(screen.getByLabelText(/start from a jd template/i), 'tpl-analyst');

    expect(screen.getByLabelText(/position title/i)).toHaveValue('Credit Analyst');
  });

  it('hides the template picker entirely when there are none, rather than an empty dropdown', async () => {
    apiMock.mockImplementation(async (path: string) => {
      if (path === '/departments') return DEPARTMENTS;
      if (path === '/jdtemplates') return [];
      return draft();
    });

    renderCreate();

    await screen.findByRole('option', { name: 'Credit Risk' });
    expect(screen.queryByLabelText(/jd template/i)).not.toBeInTheDocument();
  });
});

describe('when the lookups fail', () => {
  it('still renders a usable form instead of crashing', async () => {
    apiMock.mockImplementation(async (path: string) => {
      if (path === '/departments' || path === '/jdtemplates') throw new Error('API 500');
      return draft();
    });

    renderCreate();

    // Both lookups fall back to `[]`, so the page must survive with an empty department list.
    expect(await screen.findByLabelText(/position title/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/department/i)).toBeInTheDocument();
    expect(screen.queryByLabelText(/jd template/i)).not.toBeInTheDocument();
  });

  it('leaves the department picker on its placeholder, so nothing is submitted by accident', async () => {
    apiMock.mockImplementation(async (path: string) => {
      if (path === '/departments') throw new Error('API 500');
      if (path === '/jdtemplates') return [];
      return draft();
    });

    renderCreate();

    const select = (await screen.findByLabelText(/department/i)) as HTMLSelectElement;
    expect(select.value).toBe('');
    expect(select).toBeRequired();
  });
});
