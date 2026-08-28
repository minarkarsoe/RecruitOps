import { describe, expect, it, vi, afterEach } from 'vitest';
import { useState } from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ApplicationFormField } from '@recruitops/types';
import { FormFieldBuilder } from './FormFieldBuilder';

/*
 * `FormFieldBuilder` writes a schema that a DIFFERENT process validates — the C#
 * `ApplicationFormSchema.TryParse` in Domain — and an applicant then fills in on the public
 * site. Nothing in TypeScript checks the two agree, so this file is where that contract is
 * pinned: every assertion below quotes the rule in `ApplicationFormSchema.cs` it mirrors.
 *
 * The server's rules, for reference:
 *   key      ^[a-zA-Z0-9_]{1,50}$, unique case-insensitively
 *   label    1–100 characters, not blank
 *   type     one of text | textarea | number | date | select | checkbox
 *   select   at least 1 option, at most 50, none blank
 *   fields   at most 20
 */

// The server's key rule, copied verbatim from ApplicationFormSchema.KeyPattern.
const SERVER_KEY_PATTERN = /^[a-zA-Z0-9_]{1,50}$/;
const SERVER_FIELD_TYPES = ['text', 'textarea', 'number', 'date', 'select', 'checkbox'];

/** Drives the builder as a controlled component, and exposes what it last emitted. */
function Harness({ initial = null }: { initial?: string | null }) {
  const [json, setJson] = useState<string | null>(initial);
  return (
    <>
      <FormFieldBuilder json={json} onChange={setJson} />
      <output data-testid="emitted">{json === null ? '<<null>>' : json}</output>
    </>
  );
}

function emitted(): string | null {
  const raw = screen.getByTestId('emitted').textContent ?? '';
  return raw === '<<null>>' ? null : raw;
}

function emittedFields(): ApplicationFormField[] {
  const raw = emitted();
  return raw === null ? [] : JSON.parse(raw);
}

function schemaOf(fields: ApplicationFormField[]): string {
  return JSON.stringify(fields);
}

afterEach(() => vi.restoreAllMocks());

describe('what the builder emits', () => {
  it('emits null — not "[]" — when the last field is removed', async () => {
    const user = userEvent.setup();
    render(<Harness initial={schemaOf([
      { key: 'only', label: 'Only question', type: 'text', required: false },
    ])} />);

    await user.click(screen.getByRole('button', { name: /remove/i }));

    // `TryParse` treats null/blank as "no custom fields" and returns early. An empty array
    // would also parse, but null is what the column should hold — "[]" is a schema that
    // says nothing, stored forever.
    expect(emitted()).toBeNull();
  });

  it('round-trips: what it emits parses back to what is on screen', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.click(screen.getByRole('button', { name: /add question/i }));
    await user.type(screen.getByPlaceholderText(/question shown to the applicant/i), 'Years of experience');

    const fields = emittedFields();
    expect(fields).toHaveLength(1);
    expect(fields[0].label).toBe('Years of experience');
    expect(fields[0].type).toBe('text');
    expect(fields[0].required).toBe(false);
  });

  it('offers exactly the six types the server accepts, and no others', () => {
    render(<Harness initial={schemaOf([
      { key: 'k', label: 'A question', type: 'text', required: false },
    ])} />);

    const select = screen.getByDisplayValue('Short text') as HTMLSelectElement;
    const values = [...select.options].map((o) => o.value);

    // A seventh option here would be a type `ApplicationFormSchema.FieldTypes` rejects,
    // and the recruiter would only find out on save.
    expect(values.sort()).toEqual([...SERVER_FIELD_TYPES].sort());
  });
});

describe('the generated key — the JSONB key answers are stored under', () => {
  it('matches the server key pattern', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.click(screen.getByRole('button', { name: /add question/i }));

    const [field] = emittedFields();
    expect(field.key).toMatch(SERVER_KEY_PATTERN);
  });

  it('is not editable, because changing it would orphan every answer already collected', async () => {
    const user = userEvent.setup();
    render(<Harness />);
    await user.click(screen.getByRole('button', { name: /add question/i }));

    const key = emittedFields()[0].key;
    // Only the label and the type are editable. There is deliberately no key input.
    expect(screen.queryByDisplayValue(key)).not.toBeInTheDocument();

    await user.type(screen.getByPlaceholderText(/question shown/i), 'Renamed');
    expect(emittedFields()[0].key).toBe(key);
  });

  it('⚠️ COLLIDES when two questions are added inside the same millisecond', async () => {
    // The key is `field_${Date.now().toString(36)}`, so its uniqueness is millisecond
    // resolution and nothing else. The server rejects the WHOLE schema with
    // "Field key '…' is used more than once." — the recruiter loses the save, not one field.
    //
    // This is a real defect, pinned here rather than fixed: the fix (a counter or a random
    // suffix) changes the key format, and keys are persisted in JSONB answers, so it is a
    // decision about existing data rather than a tidy-up.
    //
    // `Date.now` is stubbed rather than the whole clock — `vi.useFakeTimers()` deadlocks
    // `userEvent`, which waits on real timers between events.
    vi.spyOn(Date, 'now').mockReturnValue(1_800_000_000_000);
    const user = userEvent.setup();

    render(<Harness />);
    await user.click(screen.getByRole('button', { name: /add question/i }));
    await user.click(screen.getByRole('button', { name: /add question/i }));

    const keys = emittedFields().map((f) => f.key);
    expect(keys).toHaveLength(2);
    expect(keys[0]).toBe(keys[1]); // ← the defect. Flip to .not.toBe when it is fixed.
  });
});

describe('schemas the builder can emit that the server will reject', () => {
  // Both of these are reachable in two clicks and produce a save that fails server-side.
  // Pinned so the behaviour is a known quantity rather than a surprise.

  it('⚠️ a freshly added question has a BLANK label, which TryParse rejects', async () => {
    const user = userEvent.setup();
    render(<Harness />);

    await user.click(screen.getByRole('button', { name: /add question/i }));

    // Server: "Field '…' needs a label of 1–100 characters."
    expect(emittedFields()[0].label).toBe('');
  });

  it('⚠️ switching a question to Dropdown leaves it with NO options, which TryParse rejects', async () => {
    const user = userEvent.setup();
    render(<Harness initial={schemaOf([
      { key: 'k1', label: 'Preferred site', type: 'text', required: false },
    ])} />);

    await user.selectOptions(screen.getByDisplayValue('Short text'), 'select');

    // Server: "Field '…' is a dropdown, so it needs at least one option."
    const [field] = emittedFields();
    expect(field.type).toBe('select');
    expect(field.options ?? []).toHaveLength(0);
  });
});

describe('dropdown options', () => {
  it('lets a comma survive being TYPED, so a second choice can be entered at all', async () => {
    // This is a regression test for a real defect found 2026-08-28. The input used to take its
    // value from `options.join(', ')`, so each keystroke was split, filtered and re-joined
    // before the next — a typed comma was parsed into a separator, dropped as a blank, and
    // erased under the cursor. Typing "Yangon, Mandalay" yielded the ONE option
    // "YangonMandalay", and the Dropdown type could not be configured by typing at all.
    const user = userEvent.setup();
    render(<Harness initial={schemaOf([
      { key: 'k1', label: 'Preferred site', type: 'select', required: false, options: [] },
    ])} />);

    await user.type(screen.getByPlaceholderText(/choices, separated by commas/i), 'Yangon, Mandalay');

    expect(emittedFields()[0].options).toEqual(['Yangon', 'Mandalay']);
    // The raw text is preserved as typed, commas and all.
    expect(screen.getByPlaceholderText(/choices/i)).toHaveValue('Yangon, Mandalay');
  });

  it('trims and drops blanks — so the server never sees a blank option', async () => {
    const user = userEvent.setup();
    render(<Harness initial={schemaOf([
      { key: 'k1', label: 'Preferred site', type: 'select', required: false, options: [] },
    ])} />);

    const input = screen.getByPlaceholderText(/choices, separated by commas/i);
    await user.click(input);
    await user.paste(' Yangon ,, Mandalay ,');

    // Server: "Field '…' has a blank option." — filtered here so it cannot happen.
    expect(emittedFields()[0].options).toEqual(['Yangon', 'Mandalay']);
  });

  it('keeps a dangling comma in the box without writing a blank option to the schema', async () => {
    const user = userEvent.setup();
    render(<Harness initial={schemaOf([
      { key: 'k1', label: 'Preferred site', type: 'select', required: false, options: [] },
    ])} />);

    await user.type(screen.getByPlaceholderText(/choices/i), 'Yangon,');

    // Mid-typing the text has a trailing separator, but the stored schema stays valid.
    expect(screen.getByPlaceholderText(/choices/i)).toHaveValue('Yangon,');
    expect(emittedFields()[0].options).toEqual(['Yangon']);
  });

  it('shows the options field only for a dropdown', async () => {
    const user = userEvent.setup();
    render(<Harness initial={schemaOf([
      { key: 'k1', label: 'A question', type: 'text', required: false },
    ])} />);

    expect(screen.queryByPlaceholderText(/choices/i)).not.toBeInTheDocument();
    await user.selectOptions(screen.getByDisplayValue('Short text'), 'select');
    expect(screen.getByPlaceholderText(/choices/i)).toBeInTheDocument();
  });
});

describe('ordering — the order questions are asked in', () => {
  const three = schemaOf([
    { key: 'a', label: 'First', type: 'text', required: false },
    { key: 'b', label: 'Second', type: 'text', required: false },
    { key: 'c', label: 'Third', type: 'text', required: false },
  ]);

  it('moves a question down, and the emitted order changes with it', async () => {
    const user = userEvent.setup();
    render(<Harness initial={three} />);

    await user.click(screen.getAllByRole('button', { name: '↓' })[0]);

    expect(emittedFields().map((f) => f.label)).toEqual(['Second', 'First', 'Third']);
  });

  it('moves a question up', async () => {
    const user = userEvent.setup();
    render(<Harness initial={three} />);

    await user.click(screen.getAllByRole('button', { name: '↑' })[2]);

    expect(emittedFields().map((f) => f.label)).toEqual(['First', 'Third', 'Second']);
  });

  it('disables the arrows at the ends rather than silently doing nothing', () => {
    render(<Harness initial={three} />);

    const ups = screen.getAllByRole('button', { name: '↑' });
    const downs = screen.getAllByRole('button', { name: '↓' });

    expect(ups[0]).toBeDisabled();
    expect(ups[2]).toBeEnabled();
    expect(downs[2]).toBeDisabled();
    expect(downs[0]).toBeEnabled();
  });

  it('removes the right question, not the one at the same index after a reorder', async () => {
    const user = userEvent.setup();
    render(<Harness initial={three} />);

    await user.click(screen.getAllByRole('button', { name: /remove/i })[1]);

    expect(emittedFields().map((f) => f.label)).toEqual(['First', 'Third']);
  });
});

describe('the 20-field ceiling mirrors ApplicationFormSchema.MaxFields', () => {
  const many = (n: number) =>
    schemaOf(
      Array.from({ length: n }, (_, i) => ({
        key: `k${i}`, label: `Q${i}`, type: 'text' as const, required: false,
      })),
    );

  it('still offers "Add question" at 19', () => {
    render(<Harness initial={many(19)} />);
    expect(screen.getByRole('button', { name: /add question/i })).toBeInTheDocument();
  });

  it('hides "Add question" at 20, so the server bound cannot be exceeded from the UI', () => {
    render(<Harness initial={many(20)} />);
    // Server: "An application form can have at most 20 custom fields."
    expect(screen.queryByRole('button', { name: /add question/i })).not.toBeInTheDocument();
  });
});

describe('malformed input', () => {
  it('renders the empty state rather than blanking, when the stored schema is not JSON', () => {
    render(<Harness initial={'{ not json'} />);

    // `parseFormFields` swallows bad JSON by contract.
    expect(screen.getByText(/no extra questions/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /add question/i })).toBeInTheDocument();
  });

  it('renders the empty state for a null schema', () => {
    render(<Harness initial={null} />);
    expect(screen.getByText(/no extra questions/i)).toBeInTheDocument();
  });
});
