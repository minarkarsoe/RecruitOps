# Challenge Report — Milestone 2 (Candidate 360 Smart Match & Executive Summary UI)

## Challenge Summary

**Overall risk assessment**: CRITICAL

**Explicit Verdict**: `REQUEST_CHANGES`

The Candidate 360 AI UI components implemented for Milestone 2 contain a critical JSX nesting syntax error in `CandidateSlideOver.tsx` that causes `npm run test` in `frontend/internal` to fail during Esbuild compilation (`Unexpected closing "SheetHeader" tag does not match opening "Tabs" tag`). Additionally, a logic bug in `getMatchBadgeConfig` incorrectly maps `LowMatch` recommendations to `Strong Match` green badges when the numeric score is high.

---

## Empirical Verification Results

1. **Typecheck (`npm run typecheck`)**: PASSED (0 errors across all workspaces).
2. **Frontend Test Suite (`npm run test` in `frontend/internal`)**: FAILED.
   - Esbuild JSX syntax error in `CandidateSlideOver.tsx`: `Unexpected closing "SheetHeader" tag does not match opening "Tabs" tag`.
   - Test runner failure: 1 test file failed out of 37 test files (3 failing tests in `CandidateSlideOverChallengerM3.test.tsx`).

---

## Detailed Findings & Challenges

### 1. [CRITICAL] Malformed JSX Nesting Hierarchy in `CandidateSlideOver.tsx`
- **File**: `frontend/internal/src/features/pipeline/CandidateSlideOver.tsx` (lines 482-664)
- **Observation**:
  - Line 482 opens `<Tabs value={activeTab} onValueChange={setActiveTab}>` inside `<SheetHeader>`.
  - Line 498 closes `</SheetHeader>` while `<Tabs>` remains open across the component boundary.
  - Line 501 opens `<SheetBody className="flex-1 overflow-y-auto">`.
  - Line 663 closes `</Tabs>` inside `<SheetBody>`.
  - Line 664 closes `</SheetBody>`.
- **Logic Chain**:
  `<SheetHeader>` and `<SheetBody>` are sibling layout elements. Opening `<Tabs>` inside `<SheetHeader>` and closing it inside `<SheetBody>` violates JSX element hierarchy rules.
- **Impact**: Esbuild fails to parse/compile `CandidateSlideOver.tsx`, breaking `npm run test` with: `Unexpected closing "SheetHeader" tag does not match opening "Tabs" tag`.
- **Mitigation**: Move the opening `<Tabs>` tag outside `<SheetHeader>` so it wraps both `<SheetHeader>` and `<SheetBody>`, or keep `<Tabs>` boundaries properly scoped per layout section.

### 2. [HIGH] Logic Bug in `getMatchBadgeConfig` Score & Recommendation Mapping
- **File**: `frontend/internal/src/features/pipeline/SmartMatchBreakdown.tsx` (lines 14-29)
- **Observation**:
  ```ts
  export function getMatchBadgeConfig(
    recommendation?: CandidateMatchAnalysis['recommendation'],
    overallScore?: number
  ): { variant: 'success' | 'primary' | 'warning' | 'danger'; label: string } {
    const score = overallScore ?? 0;
    if (recommendation === 'StrongMatch' || score >= 80) {
      return { variant: 'success', label: recommendation ? 'Strong Match' : `${score}% Match` };
    }
    ...
  ```
- **Logic Chain**:
  If the backend AI returns `recommendation: 'LowMatch'` and `overallScore: 85`, the `score >= 80` condition evaluates to `true`. Line 20 executes: `label: recommendation ? 'Strong Match' : `${score}% Match``. Because `recommendation` ('LowMatch') is a non-empty truthy string, `recommendation ? 'Strong Match'` evaluates to `'Strong Match'`.
- **Impact**: A candidate evaluated by AI as `'LowMatch'` with an 85% score will be rendered with a green `success` badge labeled `'Strong Match'`, completely misrepresenting the AI analysis to the recruiter!
- **Mitigation**: Check the exact `recommendation` enum value (e.g. `recommendation === 'StrongMatch'`) rather than checking general string truthiness `recommendation ? 'Strong Match' : ...`.

### 3. [MEDIUM] Redundant Label Formatting in `SmartMatchBreakdown.tsx`
- **File**: `frontend/internal/src/features/pipeline/SmartMatchBreakdown.tsx` (line 88)
- **Observation**:
  Line 88 renders:
  `<Badge variant={badgeConfig.variant}>{analysis?.overallScore}% Match ({badgeConfig.label})</Badge>`
  When `recommendation` is undefined, `badgeConfig.label` returns `${score}% Match` (e.g. `'85% Match'`).
- **Impact**: The UI renders: `85% Match (85% Match)`.
- **Mitigation**: Adjust label rendering logic in `SmartMatchBreakdown.tsx` so `${score}% Match` is not duplicated when `recommendation` is not provided.

### 4. [PASS] Executive Summary Panel Language Toggling & Export Actions
- **Observation**:
  - Toggling between `EN`, `MY`, and `Bilingual` correctly sets the `language` state parameter passed to `aiApi.generateExecutiveSummary`.
  - When `isBilingual: true`, the `<Badge variant="cyan">Burmese Enabled</Badge>` is properly displayed in the panel header.
  - "Copy Summary" formats headline, summary, key strengths, and interview questions, sending the formatted string to `navigator.clipboard.writeText` and toggling "Copied!" feedback state.
  - "Export (.md)" constructs a valid markdown Blob and triggers DOM download via Object URL.

### 5. [PASS] 402 API Key Unconfigured Alert Banner Behavior
- **Observation**:
  - Returning HTTP 402 Payment Required from `aiApi.matchCandidate` displays `data-testid="smart-match-402-banner"` with "AI Features Unconfigured: API key required".
  - Returning HTTP 402 Payment Required from `aiApi.generateExecutiveSummary` displays `data-testid="executive-summary-402-banner"`.
  - Candidate 360 SlideOver drawer navigation (Overview, CV Viewer, Stage History, Scorecards, Notes) remains 100% interactive and operational when API keys are unconfigured.

---

## Stress Test Results

| # | Test Case | Scenario | Expected Behavior | Actual Behavior | Pass/Fail |
|---|---|---|---|---|---|
| 1 | `npm run typecheck` compilation | Verify TypeScript compilation | 0 errors across workspace | 0 errors | PASS |
| 2 | `npm run test` execution in `frontend/internal` | Verify test suite execution | All test suites pass cleanly | Esbuild JSX error in `CandidateSlideOver.tsx` | FAIL |
| 3 | M2 Empirical Challenge Suite | Run AI UI stress tests | 11 passed (in isolated test runner) | PASS | PASS |
| 4 | `getMatchBadgeConfig('LowMatch', 85)` | Score 85 with LowMatch recommendation | Returns Low Match danger badge | Returns Strong Match success badge | FAIL |
| 5 | Executive Summary language switcher (`en`/`my`/`bilingual`) | Switch languages and generate | Sent `language: 'en'`, `'my'`, `'bilingual'` | Sent correct parameters | PASS |
| 6 | Copy to clipboard & Export markdown | Click Copy and Export | Clipboard written, `.md` downloaded | Formatted string copied, blob exported | PASS |
| 7 | HTTP 402 API key unconfigured fallback | Mock 402 error from AI endpoints | Alert banner shown, UI interactive | Banner shown, drawer fully functional | PASS |

## Unchallenged Areas

- Backend AI Controller logic and Gemini/Claude API clients (verified in Milestone 1).
