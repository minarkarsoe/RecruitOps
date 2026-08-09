# DISPATCH — 2026-08-07T13:17:00Z

## Sprint 0 Infrastructure Foundation Tasks
Received task from Sentinel (Conversation ID: cfc6b3c5-95b2-4d61-83cf-635993aeb66d).

### Requirements:
1. **R1: Object Storage Abstraction**
   - Create `IFileStorage` interface in Application layer (upload, download, delete, presigned-URL generation).
   - Create S3-compatible implementation in Infrastructure (works with MinIO for local dev & Cloudflare R2).
   - Configure via environment variables (endpoint, bucket, credentials). Ensure MinIO container in `docker-compose.yml` is usable.
   - Maintain 228 existing backend tests green, add at least 3 new tests for storage operations.

2. **R2: Myanmar Script Normalization (Zawgyi -> Unicode)**
   - In-process normalization service (Zawgyi detection + Unicode NFC conversion).
   - Expose as injectable service in Application layer. No network dependency.
   - Maintain 228 existing backend tests green, add at least 5 new unit tests (pure Unicode, Zawgyi input, mixed, null/empty, real Burmese sentence).

3. **R3: Refresh Token Mechanism**
   - Implement `POST /api/auth/refresh` endpoint returning access + refresh token pair.
   - Persist refresh tokens server-side (DB entity + EF migration) with revocation support.
   - Handle 401 for expired/revoked tokens.
   - Update `@recruitops/types` shared package and frontend `auth.ts` to attempt silent refresh.
   - Maintain 228 backend tests green + 189 frontend tests green + 0 typecheck errors, add at least 5 new tests covering refresh token behavior.

### Verification Guardrails:
- `dotnet test backend/RecruitOps.sln` (228 existing tests + new tests pass)
- `npm run test` in `frontend/internal` (189 existing tests pass)
- `npm run typecheck` (0 errors)
- `docker compose up --build` runs cleanly
