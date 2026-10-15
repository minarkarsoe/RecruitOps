# Handoff Report — AI Document Preparation & Burmese Translation UI

**Agent ID:** Explorer 3 (Frontend Document Prep & Translation UI Specialist)  
**Milestone:** Person B - Flow 2: AI Integration Flow  
**Target Path:** `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\explorer_frontend_doc_trans\handoff.md`  
**Date:** 2026-08-11  

---

## 1. Observation

- **Project Requirements (`ORIGINAL_REQUEST.md`)**:
  - R3 requires `AiDocumentPrepModal.tsx` on Candidate 360 / Job Posting pages allowing recruiters to generate and preview Interview Kits / Dossiers.
  - R3 requires an inline "Translate (EN ↔ MY)" button on long text fields (Job Descriptions, Candidate Notes) per ADR-0009.
- **Architectural & Design Constraints (`ADR-0008` & `ADR-0009`)**:
  - ADR-0008: AI optional, API-key gated. If key is missing, endpoints return `402 Payment Required` or feature-disabled response without 500 server crashes. Human confirmation is mandatory before DB mutation.
  - ADR-0009: Burmese script handling requires Unicode normalization, `Noto Sans Myanmar` fallback font, and `1.7` line-height (`leading-[1.7]`).
- **Existing Frontend Layout & Component Infrastructure**:
  - Modal primitive: `Dialog.tsx` (`Dialog`, `DialogHeader`, `DialogTitle`, `DialogDescription`, `DialogBody`, `DialogFooter`) in `packages/ui/src/Dialog.tsx`.
  - Drawer primitive: `Sheet.tsx` (`Sheet`, `SheetHeader`, `SheetTitle`, `SheetBody`, `SheetFooter`) in `packages/ui/src/Sheet.tsx`.
  - Additional primitives in `packages/ui/src/`: `Button`, `Input`, `Select`, `Tabs`, `Badge`, `Skeleton`.
  - API Client: `frontend/internal/src/lib/api.ts` exposing `aiApi` with `prepareDocument` and `translateBurmese` methods, throwing `ApiError(status, message)`.
  - Candidate 360 view: `CandidateSlideOver.tsx` in `frontend/internal/src/features/pipeline/CandidateSlideOver.tsx`.
  - Notes thread: `ApplicationNotes.tsx` in `frontend/internal/src/components/ApplicationNotes.tsx`.
  - Design system preset: `packages/ui/tailwind-preset.js` lines 51-54 defines `fontFamily.sans` with `'Noto Sans Myanmar'`.

---

## 2. Logic Chain

1. **Modal Architecture (`AiDocumentPrepModal.tsx`)**:
   - Observations show `packages/ui/src/Dialog.tsx` provides accessible modal primitives with backdrop blurring, escape key detection, and size options (`xl`).
   - By creating `AiDocumentPrepModal.tsx` using `Dialog`, we maintain complete design system consistency.
   - The modal takes `candidateId`, `jobPostingId`, `candidateName`, `jobTitle`, and `defaultDocumentType`.
   - It supports 4 document types (`InterviewKit`, `ClientDossier`, `OfferLetter`, `JobDescription`) and 3 language targets (`en`, `my`, `bilingual`).
   - Mode switcher tabs (`Formatted Preview`, `Raw Markdown`, `HTML Output`) allow recruiters to review formatted document structures before export.

2. **Inline Translation Architecture (`InlineTranslator.tsx` & `TranslatedTextField.tsx`)**:
   - Observations from ADR-0009 require non-destructive translation and `1.7` line-height with `Noto Sans Myanmar` font fallback.
   - `InlineTranslator.tsx` provides a button calling `aiApi.translateBurmese`.
   - `TranslatedTextField.tsx` wraps long text fields (Job Descriptions, Candidate Notes) and provides a tabbed toggle (`Original`, `Translated`, `Bilingual`) without mutating underlying database state.
   - Burmese translation confidence score is rendered via a `Badge` component (e.g. `96% Match`).

3. **API Key Gating & Error Handling (402 Payment Required)**:
   - Per ADR-0008, backend endpoints return HTTP 402 if API keys are unconfigured.
   - `apiFetch` in `frontend/internal/src/lib/api.ts` line 97 throws `ApiError(402, message)`.
   - Components catch `ApiError` with `status === 402` and transition state to `disabled_402`, displaying an inline warning banner ("AI Features Unconfigured: Gemini API Key missing") rather than unhandled errors.

4. **Vitest Verification Strategy**:
   - Built on existing Vitest pattern in `frontend/internal/src/features/pipeline/__tests__/CandidateSlideOver.test.tsx` and `frontend/internal/src/lib/ai.test.ts`.
   - Mocks `aiApi.prepareDocument` and `aiApi.translateBurmese` using `vi.mocked()`.
   - Tests cover component rendering, API interactions, preview tab switching, clipboard copying, and 402 error fallbacks.

---

## 3. Caveats

- **No Source Code Mutated**: Per Explorer read-only role guidelines, all findings, designs, TSX component specifications, and Vitest test specifications have been authored inside `.agents/explorer_frontend_doc_trans/analysis.md`. Application code in `frontend/internal` has not been mutated.
- **Rich Text / Markdown Parser**: The formatted preview tab in `AiDocumentPrepModal` uses `dangerouslySetInnerHTML={{ __html: result.htmlContent }}` relying on server-sanitized HTML from the Gemini endpoint, identical to `ApplicationNotes.tsx` line 35.

---

## 4. Conclusion

The design for `AiDocumentPrepModal.tsx` and inline translation components (`InlineTranslator.tsx` / `TranslatedTextField.tsx`) is fully specified, aligned with ADR-0008, ADR-0009, and the "Clear Pipeline" design system, and ready for immediate implementation by Implementer agents.

Full technical details, TSX code specifications, and Vitest test suites are available in:
`c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\explorer_frontend_doc_trans\analysis.md`

---

## 5. Verification Method

1. **Inspect Analysis File**:
   - Verify `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\explorer_frontend_doc_trans\analysis.md` contains full design details, TSX specifications for `AiDocumentPrepModal.tsx`, `InlineTranslator.tsx`, `TranslatedTextField.tsx`, state machine lifecycle, 402 error fallback, and Vitest test specs.
2. **TypeScript Typecheck Command**:
   - Run `npm run typecheck` in project root to confirm existing workspace types remain 100% clean (0 errors).
3. **Frontend Vitest Test Command**:
   - Run `npm run test` inside `frontend/internal` to confirm all 295 existing frontend tests pass cleanly.
