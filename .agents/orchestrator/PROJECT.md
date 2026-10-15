# Project: RecruitOps Sprint 0 Infrastructure Foundation

## Architecture
- **Clean Architecture (.NET 10 LTS)**: `backend/src/Domain`, `backend/src/Application`, `backend/src/Infrastructure`, `backend/src/Api`.
- **Shared Types Package**: `packages/types`.
- **Frontend CRM**: `frontend/internal`.

## Feature Inventory
| # | Feature | Description | Milestone | Source |
|---|---------|-------------|-----------|--------|
| 1 | IFileStorage Interface & DTOs | Application layer storage abstraction (Upload, Download, Delete, PresignedUrl) | M1 | ORIGINAL_REQUEST R1 |
| 2 | S3FileStorage & MinIO/R2 Config | S3-compatible storage service in Infrastructure with MinIO local & Cloudflare R2 support | M1 | ORIGINAL_REQUEST R1 |
| 3 | Storage Unit & Integration Tests | 3+ tests covering storage operations & service DI registration | M1 | ORIGINAL_REQUEST R1 |
| 4 | IMyanmarScriptNormalizer Interface | Application layer normalization service contract | M2 | ORIGINAL_REQUEST R2 |
| 5 | MyanmarScriptNormalizer Implementation | In-process Zawgyi detection + regex conversion + Unicode NFC normalization | M2 | ORIGINAL_REQUEST R2 |
| 6 | MyanmarScriptNormalizer Unit Tests | 5+ unit tests covering pure Unicode, Zawgyi, mixed, empty/null, Burmese sentence | M2 | ORIGINAL_REQUEST R2 |
| 7 | RefreshToken Entity & EF Migration | Domain entity `RefreshToken`, DbContext mapping, EF migration | M3 | ORIGINAL_REQUEST R3 |
| 8 | Auth Refresh & Revocation Service/Endpoints | `POST /api/auth/refresh` & `revoke`, token rotation, revocation, 401 handling | M3 | ORIGINAL_REQUEST R3 |
| 9 | Shared Types & Frontend Silent Refresh | Update `@recruitops/types` and frontend `auth.ts` for silent refresh | M3 | ORIGINAL_REQUEST R3 |
| 10| Refresh Token Integration Tests | 5+ backend tests covering valid, expired, revoked, reuse detection, login pair | M3 | ORIGINAL_REQUEST R3 |
| 11| Cross-Cutting E2E Verification | All 228+ backend + 189+ frontend tests pass, 0 typecheck errors, docker build clean | M4 | ORIGINAL_REQUEST Verification |

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| M1 | Object Storage Abstraction (R1) | `IFileStorage` interface, `S3FileStorage` implementation, options/appsettings, docker-compose, 3+ tests | None | DONE |
| M2 | Myanmar Script Normalization (R2) | `IMyanmarScriptNormalizer` interface, in-process Zawgyi->Unicode converter, DI, 5+ unit tests | None | DONE |
| M3 | Refresh Token Mechanism (R3) | `RefreshToken` DB entity & migration, `POST /api/auth/refresh`, revocation, 5+ tests, `@recruitops/types` & `auth.ts` | None | PLANNED |
| M4 | Final E2E Integration & Verification | Backend + frontend test execution, typecheck, docker build validation | M1, M2, M3 | PLANNED |

## Interface Contracts
### Application Layer Storage Abstraction (M1) - [DONE]
- Interface: `IFileStorage` (`backend/src/Application/Interfaces/IFileStorage.cs`)
- Operations:
  - `Task<UploadFileResponse> UploadAsync(UploadFileRequest request, CancellationToken cancellationToken = default)`
  - `Task<StorageObject?> DownloadAsync(string fileKey, CancellationToken cancellationToken = default)`
  - `Task<bool> DeleteAsync(string fileKey, CancellationToken cancellationToken = default)`
  - `Task<string> GetPresignedUrlAsync(PresignedUrlRequest request, CancellationToken cancellationToken = default)`
  - `Task<bool> ExistsAsync(string fileKey, CancellationToken cancellationToken = default)`

### Application Layer Myanmar Script Normalizer (M2) - [DONE]
- Interface: `IMyanmarScriptNormalizer` (`backend/src/Application/Interfaces/IMyanmarScriptNormalizer.cs`)
- Operations:
  - `string Normalize(string? input)`
  - `bool IsZawgyi(string? input)`

### Auth Refresh Token Interface & DTOs (M3)
- Endpoint: `POST /api/auth/refresh`
  - Request: `{ refreshToken: string }`
  - Response: `{ accessToken: string, refreshToken: string, tokenType: "Bearer", expiresIn: number }`
- Shared Types (`packages/types/src/index.ts`):
  - `RefreshRequest`, updated `LoginResponse` / `AuthResponse`.

## Code Layout
- `backend/src/Application/Interfaces/IFileStorage.cs`
- `backend/src/Application/DTOs/StorageDtos.cs`
- `backend/src/Infrastructure/Services/FileStorage/S3FileStorage.cs`
- `backend/src/Infrastructure/Options/FileStorageOptions.cs`
- `backend/src/Application/Interfaces/IMyanmarScriptNormalizer.cs`
- `backend/src/Infrastructure/Services/MyanmarScript/MyanmarScriptNormalizer.cs`
- `backend/src/Domain/Entities/RefreshToken.cs`
- `backend/src/Infrastructure/Persistence/Migrations/`
- `backend/src/Application/Services/AuthService.cs`
- `backend/src/Api/Controllers/AuthController.cs`
- `packages/types/src/`
- `frontend/internal/src/services/auth.ts`
