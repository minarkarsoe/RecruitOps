#!/usr/bin/env node
// Trello board driver for RecruitOps project management.
//
// Credentials live in `.env.trello` at the repo root, which `.gitignore:20` (`.env.*`)
// already excludes — verified with `git check-ignore`. Nothing in this script prints the
// key or the token, and no request URL is ever logged, because Trello passes both as query
// parameters and a logged URL would leak them.
//
// Setup (you do this once, yourself):
//
//   1. Get a key + token at https://trello.com/power-ups/admin  →  your Power-Up  →  API key.
//      The token is generated from the "Token" link next to the key.
//   2. Create `.env.trello` in the repo root with:
//
//        TRELLO_KEY=...
//        TRELLO_TOKEN=...
//        TRELLO_BOARD_ID=...      # optional; `boards` prints it for you
//
//   3. `node scripts/trello.mjs boards` to find the board id, then put it in the file.
//
// Usage:
//   node scripts/trello.mjs boards                       list your boards + their ids
//   node scripts/trello.mjs lists                        list the board's columns + ids
//   node scripts/trello.mjs init                         create the working-process columns
//   node scripts/trello.mjs card "<column>" "<title>" [--desc "..."]
//   node scripts/trello.mjs move <cardId> "<column>"
//
// Requires Node 18+ for global fetch. No dependencies.

import { readFileSync, existsSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const ENV_FILE = resolve(ROOT, '.env.trello');

/** The columns `init` creates, in order.
 *
 *  Two of these are here for reasons specific to this repo rather than generic kanban:
 *
 *  - "Needs Human Sign-off" exists because CLAUDE.md requires explicit human review for any
 *    change touching authentication, authorization or payment logic. Without a column for it,
 *    that review is a promise nobody can see the state of.
 *  - "Blocked" exists because this project accumulates decisions waiting on you — the OCR
 *    build-vs-buy call, the VPS provider — and a card sitting in "In Progress" for a week
 *    misreports why it has not moved.
 *
 *  Edit this list and re-run `init`; it only creates what is missing.
 */
const WORKING_PROCESS = [
  'Backlog',
  'Ready',
  'In Progress',
  'In Review',
  'Needs Human Sign-off',
  'Blocked',
  'Done',
];

// ---------- credentials ----------

/** Minimal .env parser. Strips CR so a CRLF file on Windows does not append \r to the token —
 *  which fails as a 401 that looks like a wrong key rather than a line-ending problem. */
function loadEnvFile(path) {
  if (!existsSync(path)) return {};
  const out = {};
  for (const rawLine of readFileSync(path, 'utf8').split('\n')) {
    const line = rawLine.replace(/\r$/, '').trim();
    if (!line || line.startsWith('#')) continue;
    const eq = line.indexOf('=');
    if (eq === -1) continue;
    const key = line.slice(0, eq).trim();
    let value = line.slice(eq + 1).trim();
    if ((value.startsWith('"') && value.endsWith('"')) ||
        (value.startsWith("'") && value.endsWith("'"))) {
      value = value.slice(1, -1);
    }
    out[key] = value;
  }
  return out;
}

const fileEnv = loadEnvFile(ENV_FILE);
const KEY = process.env.TRELLO_KEY || fileEnv.TRELLO_KEY;
const TOKEN = process.env.TRELLO_TOKEN || fileEnv.TRELLO_TOKEN;
const BOARD_ID = process.env.TRELLO_BOARD_ID || fileEnv.TRELLO_BOARD_ID;

function requireCredentials() {
  if (KEY && TOKEN) return;
  console.error(
    `Missing TRELLO_KEY / TRELLO_TOKEN.\n\n` +
    `Create ${ENV_FILE} containing:\n\n` +
    `  TRELLO_KEY=your-key\n` +
    `  TRELLO_TOKEN=your-token\n` +
    `  TRELLO_BOARD_ID=your-board-id\n\n` +
    `That file is already covered by .gitignore (.env.*), so it will not be committed.\n` +
    `Get both at https://trello.com/power-ups/admin`
  );
  process.exit(1);
}

function requireBoard() {
  if (BOARD_ID) return BOARD_ID;
  console.error(
    `Missing TRELLO_BOARD_ID.\n\n` +
    `Run:  node scripts/trello.mjs boards\n` +
    `then add the id you want to ${ENV_FILE} as TRELLO_BOARD_ID=...`
  );
  process.exit(1);
}

// ---------- api ----------

/** Calls the Trello API. The key and token go in the query string, so neither the URL nor the
 *  params object is ever logged — on failure we surface the method, the path and Trello's own
 *  error text, and nothing else. */
async function api(method, path, params = {}) {
  const url = new URL(`https://api.trello.com/1${path}`);
  for (const [k, v] of Object.entries(params)) {
    if (v !== undefined && v !== null) url.searchParams.set(k, String(v));
  }
  url.searchParams.set('key', KEY);
  url.searchParams.set('token', TOKEN);

  const res = await fetch(url, { method, headers: { Accept: 'application/json' } });
  const body = await res.text();

  if (!res.ok) {
    const detail = body.slice(0, 300) || '(empty response)';
    throw new Error(`${method} ${path} -> ${res.status} ${res.statusText}: ${detail}`);
  }
  return body ? JSON.parse(body) : null;
}

async function getLists(boardId) {
  return api('GET', `/boards/${boardId}/lists`, { fields: 'name,pos', filter: 'open' });
}

/** Resolves a column by name, case-insensitively, so `"in progress"` finds "In Progress". */
async function resolveList(boardId, name) {
  const lists = await getLists(boardId);
  const wanted = name.trim().toLowerCase();
  const match = lists.find((l) => l.name.trim().toLowerCase() === wanted);
  if (!match) {
    throw new Error(
      `No column named "${name}". Available: ${lists.map((l) => l.name).join(', ') || '(none)'}`
    );
  }
  return match;
}

// ---------- commands ----------

/** Opens Trello's authorization page in the browser so you can generate a TOKEN.
 *
 *  The token is a different credential from the "Secret" shown next to the API key on the
 *  Power-Ups admin page — the Secret is the OAuth1 client secret and is not used here at all.
 *
 *  It deliberately OPENS the URL rather than printing it: the URL embeds TRELLO_KEY, so
 *  printing it would put the key in the terminal, which is exactly what this setup is trying
 *  to avoid. Nothing sensitive is written to stdout.
 */
async function cmdAuthorize() {
  if (!KEY) {
    console.error(`Set TRELLO_KEY in ${ENV_FILE} first, then run this again.`);
    process.exit(1);
  }

  const url =
    'https://trello.com/1/authorize' +
    '?expiration=never' +
    '&scope=read,write' +
    '&response_type=token' +
    '&name=RecruitOps%20PM' +
    `&key=${encodeURIComponent(KEY)}`;

  // The URL is ALWAYS printed, and opening the browser is only a convenience on top.
  //
  // An earlier version deliberately withheld the URL to keep TRELLO_KEY off the terminal,
  // and opened it with `spawn(..., { detached: true, stdio: 'ignore' }).unref()`. Two things
  // went wrong: `cmd /c start` split the URL at the first `&` (cmd treats it as a command
  // separator), and when that was fixed the detached+ignored spawn swallowed its own failure,
  // so a browser that never opened looked exactly like one that did. Withholding the URL
  // turned every launch failure into a dead end with no way forward.
  //
  // The secrecy was misplaced anyway: a Trello API key is public by design — it is embedded
  // in client-side Power-Ups. The TOKEN is the credential, and it never appears here; the
  // page gives it to you.
  try {
    const { spawnSync } = await import('node:child_process');
    const [cmd, args] =
      process.platform === 'win32'
        ? ['powershell', ['-NoProfile', '-Command', 'Start-Process', `'${url}'`]]
        : process.platform === 'darwin' ? ['open', [url]]
        : ['xdg-open', [url]];

    const r = spawnSync(cmd, args, { stdio: 'ignore' });
    console.log(
      !r.error && r.status === 0
        ? 'Opened the authorization page in your browser.\n'
        : 'Could not open a browser automatically — use the link below.\n'
    );
  } catch {
    console.log('Could not open a browser automatically — use the link below.\n');
  }

  console.log('Open this and click "Allow":\n');
  console.log(url);
  console.log(
    `\nTrello then shows a long token string. Paste it into:\n` +
    `  ${ENV_FILE}\n` +
    `as TRELLO_TOKEN=...\n\n` +
    `Revoke it any time at https://trello.com/my/account (Applications).`
  );
}

async function cmdBoards() {
  const boards = await api('GET', '/members/me/boards', { fields: 'name,url', filter: 'open' });
  if (!boards.length) {
    console.log('No open boards on this account.');
    return;
  }
  for (const b of boards) console.log(`${b.id}  ${b.name}`);
  console.log(`\nPut the id you want in ${ENV_FILE} as TRELLO_BOARD_ID=...`);
}

async function cmdLists() {
  const boardId = requireBoard();
  const lists = await getLists(boardId);
  if (!lists.length) {
    console.log('Board has no columns yet. Run: node scripts/trello.mjs init');
    return;
  }
  for (const l of lists) console.log(`${l.id}  ${l.name}`);
}

/** Idempotent: creates only the columns that are missing, so re-running after editing
 *  WORKING_PROCESS adds the new ones without duplicating what is there. */
async function cmdInit() {
  const boardId = requireBoard();
  const existing = await getLists(boardId);
  const have = new Set(existing.map((l) => l.name.trim().toLowerCase()));

  let created = 0;
  for (const name of WORKING_PROCESS) {
    if (have.has(name.toLowerCase())) {
      console.log(`= ${name} (already there)`);
      continue;
    }
    await api('POST', `/boards/${boardId}/lists`, { name, pos: 'bottom' });
    console.log(`+ ${name}`);
    created++;
  }
  console.log(`\n${created} column${created === 1 ? '' : 's'} created, ${WORKING_PROCESS.length - created} already present.`);
}

/** Archives a column by name. Reversible — Trello keeps archived lists and their cards, and
 *  they can be restored from the board menu under Archived items. There is no API call here
 *  that deletes anything. */
async function cmdArchive(args) {
  const boardId = requireBoard();
  if (!args.length) {
    console.error('Usage: node scripts/trello.mjs archive "<column>" ["<column>" ...]');
    process.exit(1);
  }

  for (const name of args) {
    const list = await resolveList(boardId, name);
    await api('PUT', `/lists/${list.id}/closed`, { value: 'true' });
    console.log(`archived  ${list.name}`);
  }
}

async function cmdCard(args) {
  const boardId = requireBoard();
  const [listName, title] = args;
  if (!listName || !title) {
    console.error('Usage: node scripts/trello.mjs card "<column>" "<title>" [--desc "..."]');
    process.exit(1);
  }
  const descFlag = args.indexOf('--desc');
  const desc = descFlag !== -1 ? args[descFlag + 1] : undefined;

  const list = await resolveList(boardId, listName);
  const card = await api('POST', '/cards', { idList: list.id, name: title, desc, pos: 'bottom' });
  console.log(`${card.id}  ${card.name}  ->  ${list.name}`);
  console.log(card.shortUrl);
}

async function cmdMove(args) {
  const boardId = requireBoard();
  const [cardId, listName] = args;
  if (!cardId || !listName) {
    console.error('Usage: node scripts/trello.mjs move <cardId> "<column>"');
    process.exit(1);
  }
  const list = await resolveList(boardId, listName);
  const card = await api('PUT', `/cards/${cardId}`, { idList: list.id });
  console.log(`${card.name}  ->  ${list.name}`);
}

// ---------- entry ----------

const [command, ...rest] = process.argv.slice(2);

const COMMANDS = {
  authorize: cmdAuthorize,
  boards: cmdBoards,
  lists: cmdLists,
  init: cmdInit,
  archive: () => cmdArchive(rest),
  card: () => cmdCard(rest),
  move: () => cmdMove(rest),
};

if (!command || !COMMANDS[command]) {
  console.error(
    `Usage: node scripts/trello.mjs <command>\n\n` +
    `  authorize                           open Trello's page to generate a TOKEN\n` +
    `  boards                              list your boards + ids\n` +
    `  lists                               list the board's columns + ids\n` +
    `  init                                create the working-process columns\n` +
    `  archive "<column>" [...]            archive one or more columns (reversible)\n` +
    `  card "<column>" "<title>" [--desc]  create a card\n` +
    `  move <cardId> "<column>"            move a card between columns`
  );
  process.exit(1);
}

// `authorize` is the one command that runs BEFORE a token exists — it is how you get one —
// so it checks for TRELLO_KEY on its own instead.
if (command !== 'authorize') requireCredentials();

try {
  await COMMANDS[command]();
} catch (err) {
  console.error(err.message);
  process.exit(1);
}
