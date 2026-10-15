# Handoff Report — Milestone 1 Adversarial Challenge

**Verdict**: **REQUEST_CHANGES**

---

## Challenge Summary

**Overall risk assessment**: **HIGH**

- **Pre-existing Integration & Unit Test Failures**: `dotnet test backend/RecruitOps.sln` fails 5 integration/unit tests in `ResumeExtractionTests`.
- **Text Extraction Heuristic Bugs**: Word boundary matching `\b` drops skills containing non-word characters (`C#`, `.NET`); candidate name heuristic misidentifies top resume section headers (`"PERSONAL DETAILS"`); experience years regex misses common inverted syntax (`"Experience: 5 years"`).
- **Zawgyi Normalization & Language Tagging Defect**: Single Zawgyi feature in a 99% English document triggers global `my-Zawgyi` language tagging.
- **Stream Handling & Memory Footprint**: Double-buffering of 9.9MB files in `MemoryStream` across `ResumeService` and `DocumentTextExtractor` creates ~30MB memory allocations on Large Object Heap per upload.

---

## Challenges

### [High] Challenge 1: Word Boundary `\b` Regex Drops Skills with Special Characters (`C#`, `.NET`)

- **Assumption challenged**: `DocumentTextExtractor.ExtractContactInfo` matches all configured `SkillKeywords`.
- **Attack scenario**: Candidate resume lists `Technical Skills: C#, .NET, React, Docker`.
  - In `DocumentTextExtractor.cs:205-208`:
    `SkillKeywords.Where(k => Regex.IsMatch(text, $@"\b{Regex.Escape(k)}\b", RegexOptions.IgnoreCase))`
  - `\b` represents a word boundary between `\w` (alphanumeric/underscore) and `\W` (non-word).
  - In `C#`, `#` is `\W`. Trailing `\b` after `#` requires the following character to be `\w`. When followed by space, comma, or end-of-line (`\W`), `\b` fails.
  - In `.NET`, leading `.` is `\W`. Leading `\b` before `.` requires the preceding character to be `\w`. When preceded by space (`\W`), `\b` fails.
- **Blast Radius**: Core skills `C#` and `.NET` are systematically dropped from `ParsedContactInfo.Skills` for all candidates.
- **Empirical Evidence**: Directly causes `DocumentTextExtractor_ParsesContactInfoHeuristics` in `ResumeExtractionTests.cs` to fail.
- **Mitigation**: Use custom boundary matching or trimmed matching that handles boundary punctuation correctly instead of raw `\b`.

### [Medium] Challenge 2: Candidate Name Extraction Misidentifies Resume Section Headers

- **Assumption challenged**: First non-CV line under 60 characters is the candidate's name.
- **Attack scenario**: Resume begins with header `PERSONAL DETAILS` or `CAREER OBJECTIVE` before candidate name.
  - In `DocumentTextExtractor.cs:188-202`:
    The line loop filters out `"Resume"`, `"Curriculum Vitae"`, `"CV"`, `@`, and `http`.
  - `PERSONAL DETAILS` passes all filters, setting `candidateName = "PERSONAL DETAILS"`.
- **Blast Radius**: Incorrect candidate name populated in application record.
- **Mitigation**: Expand blacklist of header terms (`"PERSONAL DETAILS"`, `"CAREER OBJECTIVE"`, `"SUMMARY"`, `"PROFILE"`, `"CONFIDENTIAL"`, etc.) and prefer lines containing proper name casing or preceded/followed by contact details.

### [Medium] Challenge 3: Zawgyi Over-Classification on Mixed Burmese-English Documents

- **Assumption challenged**: Zawgyi detection accurately identifies document primary language and encoding.
- **Attack scenario**: A 99% English CV contains a brief 5-character Zawgyi greeting (`မဂၤလာပါ`).
  - In `MyanmarScriptNormalizer.cs:167-174`:
    `DetectZawgyi` returns `true` as long as `zawgyiFeatureMatches > 0`.
  - `DocumentTextExtractor.cs:90` assigns `DetectedLanguage = "my-Zawgyi"`.
- **Blast Radius**: English resumes are mislabeled as Burmese Zawgyi language documents in metadata.
- **Mitigation**: Require minimum threshold of Zawgyi feature matches relative to total character count before setting primary language to `"my-Zawgyi"`.

### [High] Challenge 4: Stream Double-Buffering for 9.9MB Files

- **Assumption challenged**: File upload streaming handles maximum 10MB payload efficiently without unnecessary heap allocations.
- **Attack scenario**: Concurrent uploads of 9.9MB CV documents (e.g. 10,380,902 bytes).
  - `ApplicationsController.UploadResume` receives `IFormFile` (buffered by ASP.NET Core).
  - `ResumeService.UploadAndExtractResumeAsync` (lines 55-56) copies `file.CopyToAsync(memoryStream)`.
  - `DocumentTextExtractor.ExtractTextAsync` (lines 47-52) copies `stream.CopyToAsync(ms)` into a second `MemoryStream`.
- **Blast Radius**: Each 9.9MB upload allocates ~30MB on the Large Object Heap (LOH), triggering frequent GC pauses under concurrent candidate application loads.
- **Mitigation**: Pass original stream directly without duplicate `MemoryStream` reallocation.

---

## Stress Test Results

| Scenario | Expected Behavior | Actual Behavior | Pass/Fail |
|---|---|---|---|
| Skills `C#`, `.NET` | Extracted in `Skills` list | Dropped (`Skills` missing `C#`, `.NET`) | **FAIL** |
| Top line `PERSONAL DETAILS` | Candidate name parsed as real name | Candidate name = `"PERSONAL DETAILS"` | **FAIL** |
| Experience `"Experience: 5 years"` | Extracted as 5 years | Returns `null` | **FAIL** |
| Zawgyi greeting in 99% English CV | Extracted text normalized; `DetectedLanguage = "en"` | `DetectedLanguage = "my-Zawgyi"` | **FAIL** |
| 10.1MB PDF / DOCX file upload | Rejected with `400 BadRequest` | Returns `400 BadRequest` | **PASS** |
| 9.9MB DOCX file stream extraction | Processed within 10MB limit | Processed, double-buffered | **PASS** (Performance caveat) |
| Backend test suite `dotnet test` | All tests PASS | 5 tests FAIL in `ResumeExtractionTests` | **FAIL** |

---

## Handoff 5-Component Report

### 1. Observation

- **Backend Test Suite Command & Result**:
  - Command: `dotnet test backend/RecruitOps.sln`
  - Result: `Failed! - Failed: 5, Passed: 285, Total: 290` (existing test suite failures)
  - Failing tests in `RecruitOps.Api.Tests.ResumeExtractionTests`:
    1. `DocumentTextExtractor_ParsesContactInfoHeuristics` (Fails on `Assert.Contains("C#", parsed.Skills)`)
    2. `UploadResume_ZawgyiNormalization_NormalizesToUnicode` (`401 Unauthorized` in `CreateTestApplicationAsync()`)
    3. `UploadResume_SuccessfulDocx_Returns200AndExtractedText` (`401 Unauthorized` in `CreateTestApplicationAsync()`)
    4. `UploadResume_SuccessfulPdfOrImage_Returns200AndResultDto` (`401 Unauthorized` in `CreateTestApplicationAsync()`)
    5. `GetResume_UploadedResume_ReturnsFileStream` (`401 Unauthorized` in `CreateTestApplicationAsync()`)
- **Code Locations Inspected**:
  - `backend/src/Infrastructure/Services/DocumentExtraction/DocumentTextExtractor.cs`:
    - Line 20-21: `PhoneRegex` definition
    - Line 174-216: `ExtractContactInfo` heuristic logic
    - Line 205-208: `SkillKeywords` regex matching using `\b`
    - Line 47-52: Stream double-buffering into `ms`
  - `backend/src/Infrastructure/Services/MyanmarScript/MyanmarScriptNormalizer.cs`:
    - Line 167-175: `DetectZawgyi` logic
  - `backend/src/Infrastructure/Services/ResumeService.cs`:
    - Line 55-56: Stream buffering into `memoryStream`
  - `backend/src/Api/Controllers/ApplicationsController.cs`:
    - Line 63-66: `file.Length > 10 * 1024 * 1024` check
- **Empirical Challenger Test Suite**:
  - Created `backend/tests/RecruitOps.Api.Tests/EmpiricalMilestone1ChallengerTests.cs` to test all 4 testing tasks.

### 2. Logic Chain

1. `DocumentTextExtractor.ExtractContactInfo` uses regexes for phone numbers, skills, candidate names, and experience years.
2. `SkillKeywords` contains `"C#"` and `".NET"`. `ExtractContactInfo` wraps keywords in `\b{k}\b`. In regex, `\b` requires transition between word character `\w` and non-word character `\W`. Since `#` and `.` are `\W`, boundary checks fail against spaces or commas. `C#` and `.NET` are therefore omitted from extracted skills. This was empirically proven and causes `DocumentTextExtractor_ParsesContactInfoHeuristics` to fail.
3. Name extraction logic selects the first line under 60 chars that isn't `Resume`/`CV`. Section headers like `PERSONAL DETAILS` pass these criteria, resulting in invalid candidate names.
4. Zawgyi detection triggers `IsZawgyiDetected = true` if `zawgyiFeatureMatches > 0` regardless of text length ratio, causing mixed English resumes to be tagged as `my-Zawgyi`.
5. Stream handling correctly rejects 10.1MB files at controller level, but for 9.9MB files, double-buffers stream across `ResumeService` and `DocumentTextExtractor`, allocating ~30MB heap per upload.
6. Existing integration tests in `ResumeExtractionTests` fail with `401 Unauthorized` due to authentication/tenant header mismatch during scenario setup.

### 3. Caveats

- OCR capabilities for image-based PDFs (`ExtractFromImageOrScannedAsync`) currently return placeholder text (`"[Scanned / Image Document Extracted Text...]"`) as full OCR engine (Tesseract/AWS Textract) is deferred to future milestones.
- 10MB limit enforcement at `ApplicationsController` relies on `file.Length` provided by ASP.NET Core multipart parser.

### 4. Conclusion

**Verdict**: **REQUEST_CHANGES**

Milestone 1 fails verification due to failing unit/integration tests in the backend test suite (`dotnet test backend/RecruitOps.sln`), confirmed bugs in contact info & skill extraction heuristics, language tag over-classification on mixed documents, and redundant stream memory buffering.

### 5. Verification Method

Run the following command in the workspace root:

```powershell
dotnet test backend/RecruitOps.sln
```

- Inspect test output in `RecruitOps.Api.Tests.dll`.
- Confirm failures in `ResumeExtractionTests` and empirical tests in `EmpiricalMilestone1ChallengerTests.cs`.
- To invalidate this verdict: All tests in `dotnet test backend/RecruitOps.sln` must pass, skills matching must extract `C#` and `.NET`, and section headers must not be misparsed as candidate names.
