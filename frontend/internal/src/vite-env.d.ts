/// <reference types="vite/client" />

// Typed env vars for this app. Without this, `import.meta.env` does not exist
// as far as TypeScript is concerned (TS2339).
interface ImportMetaEnv {
  readonly VITE_API_BASE_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
