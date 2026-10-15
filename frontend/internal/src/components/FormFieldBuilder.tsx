import { useState } from 'react';
import { Button } from '@recruitops/ui';
import type { ApplicationFormField } from '@recruitops/types';
import { parseFormFields } from '@recruitops/types';

const TYPES: { value: ApplicationFormField['type']; label: string }[] = [
  { value: 'text', label: 'Short text' },
  { value: 'textarea', label: 'Long text' },
  { value: 'number', label: 'Number' },
  { value: 'date', label: 'Date' },
  { value: 'select', label: 'Dropdown' },
  { value: 'checkbox', label: 'Yes / no' },
];

const MAX_FIELDS = 20; // mirrors ApplicationFormSchema.MaxFields

/**
 * Editor for a posting's custom application questions (Module 2.2).
 *
 * The schema travels as an opaque JSON string end to end, so this component parses on the
 * way in and serialises on the way out. Keeping it a string means adding a field type is a
 * change in two places (here and the Domain validator) rather than a database migration.
 */
export function FormFieldBuilder({
  json,
  onChange,
}: {
  json: string | null | undefined;
  onChange: (json: string | null) => void;
}) {
  const fields = parseFormFields(json);

  const write = (next: ApplicationFormField[]) =>
    onChange(next.length === 0 ? null : JSON.stringify(next));

  const update = (index: number, patch: Partial<ApplicationFormField>) =>
    write(fields.map((f, i) => (i === index ? { ...f, ...patch } : f)));

  const add = () =>
    write([
      ...fields,
      // The key is generated rather than typed. It is the JSONB key answers are stored
      // under, so letting someone edit it later would orphan every answer already collected.
      { key: `field_${Date.now().toString(36)}`, label: '', type: 'text', required: false },
    ]);

  const remove = (index: number) => write(fields.filter((_, i) => i !== index));

  const move = (index: number, delta: number) => {
    const target = index + delta;
    if (target < 0 || target >= fields.length) return;
    const next = [...fields];
    [next[index], next[target]] = [next[target], next[index]];
    write(next);
  };

  const input = 'h-9 w-full rounded-md border border-line px-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-700';

  return (
    <div>
      <p className="mb-1 text-sm font-semibold">
        Extra questions <span className="font-normal text-ink-400">(optional)</span>
      </p>
      <p className="mb-3 text-sm text-ink-600">
        Asked on the public application form, in this order. Answers appear on the candidate
        in the pipeline.
      </p>

      {fields.length === 0 && (
        <p className="mb-3 text-sm text-ink-400">
          No extra questions — applicants are asked for name and contact details only.
        </p>
      )}

      <ul className="space-y-3">
        {fields.map((f, i) => (
          <li key={f.key} className="rounded-md border border-line p-3">
            <div className="flex gap-2">
              <input
                className={input} placeholder="Question shown to the applicant"
                value={f.label}
                onChange={(e) => update(i, { label: e.target.value })}
              />
              <select
                className={`${input} w-40 shrink-0`}
                value={f.type}
                onChange={(e) => update(i, { type: e.target.value as ApplicationFormField['type'] })}
              >
                {TYPES.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}
              </select>
            </div>

            {f.type === 'select' && (
              <OptionsInput
                className={`${input} mt-2`}
                options={f.options ?? []}
                onChange={(options) => update(i, { options })}
              />
            )}

            <div className="mt-2 flex items-center justify-between">
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox" checked={f.required}
                  onChange={(e) => update(i, { required: e.target.checked })}
                />
                Required
              </label>
              <div className="flex gap-1">
                <button type="button" onClick={() => move(i, -1)} disabled={i === 0}
                  className="px-2 text-sm text-ink-600 disabled:opacity-30">↑</button>
                <button type="button" onClick={() => move(i, 1)} disabled={i === fields.length - 1}
                  className="px-2 text-sm text-ink-600 disabled:opacity-30">↓</button>
                <button type="button" onClick={() => remove(i)}
                  className="px-2 text-sm font-semibold text-critical-700">Remove</button>
              </div>
            </div>
          </li>
        ))}
      </ul>

      {fields.length < MAX_FIELDS && (
        <Button variant="secondary" type="button" className="mt-3" onClick={add}>
          Add question
        </Button>
      )}
    </div>
  );
}

/**
 * The comma-separated choices for a `select`.
 *
 * ⚠️ **The raw text has to live in local state.** This input previously took its value straight
 * from `options.join(', ')`, which meant every keystroke was split, trimmed, filtered and
 * re-joined before the next one — so the moment you typed a comma it was parsed into a
 * separator, dropped as a blank entry, and **erased under the cursor**. Typing
 * "Yangon, Mandalay" produced the single option `YangonMandalay`. A recruiter could not enter
 * a second choice at all; pasting was the only way, and nobody had noticed because the
 * component had no tests. Found 2026-08-28 by the first ones.
 *
 * Seeded once per field. The `<li>` above is keyed by `f.key`, so React keeps this component's
 * identity across reorder and gives a genuinely different field a fresh one.
 */
function OptionsInput({
  options,
  onChange,
  className,
}: {
  options: string[];
  onChange: (options: string[]) => void;
  className: string;
}) {
  const [raw, setRaw] = useState(options.join(', '));

  return (
    <input
      className={className}
      placeholder="Choices, separated by commas"
      value={raw}
      onChange={(e) => {
        setRaw(e.target.value);
        // The stored schema stays clean — blanks filtered, everything trimmed — even while
        // the text being typed still has a dangling comma in it.
        onChange(e.target.value.split(',').map((o) => o.trim()).filter(Boolean));
      }}
    />
  );
}
