# Challenger Report: Milestone 1 (Single CV Upload & Extraction API)

**Verdict**: `REQUEST_CHANGES`

---

## 1. Observation

### Command Execution:
`dotnet test backend/RecruitOps.sln`

### Test Suite Execution Output:
```
Failed! - Failed: 5, Passed: 285, Skipped: 0, Total: 290, Duration: 12 s - RecruitOps.Api.Tests.dll (net10.0)
Passed! - Failed: 0, Passed: 51, Skipped: 0, Total: 51, Duration: 1 s - RecruitOps.Domain.Tests.dll (net10.0)
```

### Direct Failure Details:

1. **`RecruitOps.Api.Tests.ResumeExtractionTests.UploadResume_SuccessfulDocx_Returns200AndExtractedText`**
   - **File**: `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\backend\tests\RecruitOps.Api.Tests\ResumeExtractionTests.cs:line 111`
   - **Error**: `System.Net.Http.HttpRequestException : Response status code does not indicate success: 401 (Unauthorized).`
   - **Stack Trace**: `at RecruitOps.Api.Tests.ResumeExtractionTests.CreateTestApplicationAsync() line 83`

2. **`RecruitOps.Api.Tests.ResumeExtractionTests.UploadResume_SuccessfulPdfOrImage_Returns200AndResultDto`**
   - **File**: `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\backend\tests\RecruitOps.Api.Tests\ResumeExtractionTests.cs:line 140`
   - **Error**: `System.Net.Http.HttpRequestException : Response status code does not indicate success: 401 (Unauthorized).`
   - **Stack Trace**: `at RecruitOps.Api.Tests.ResumeExtractionTests.CreateTestApplicationAsync() line 83`

3. **`RecruitOps.Api.Tests.ResumeExtractionTests.UploadResume_ZawgyiNormalization_NormalizesToUnicode`**
   - **File**: `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\backend\tests\RecruitOps.Api.Tests\ResumeExtractionTests.cs:line 164`
   - **Error**: `System.Net.Http.HttpRequestException : Response status code does not indicate success: 401 (Unauthorized).`
   - **Stack Trace**: `at RecruitOps.Api.Tests.ResumeExtractionTests.CreateTestApplicationAsync() line 83`

4. **`RecruitOps.Api.Tests.ResumeExtractionTests.GetResume_UploadedResume_ReturnsFileStream`**
   - **File**: `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\backend\tests\RecruitOps.Api.Tests\ResumeExtractionTests.cs:line 239`
   - **Error**: `System.Net.Http.HttpRequestException : Response status code does not indicate success: 401 (Unauthorized).`
   - **Stack Trace**: `at RecruitOps.Api.Tests.ResumeExtractionTests.CreateTestApplicationAsync() line 83`

5. **`RecruitOps.Api.Tests.ResumeExtractionTests.DocumentTextExtractor_ParsesContactInfoHeuristics`**
   - **File**: `c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\backend\tests\RecruitOps.Api.Tests\ResumeExtractionTests.cs:line 274`
   - **Error**: `Assert.Equal() Failure: Strings differ`
   - **Expected**: `"+95 9 1234 5678"`
   - **Actual**: `null`

---

## 2. Logic Chain

1. **Test Failure in `CreateTestApplicationAsync()`**:
   - In `ResumeExtractionTests.cs` lines 60-75:
     ```csharp
     var postingRes = await Internal(Roles.Recruiter).PostAsJsonAsync("/api/jobpostings", ...);
     var posting = (await postingRes.Content.ReadFromJsonAsync<JobPostingDetailDto>())!;
     ...
     await Internal(Roles.Recruiter).PostAsync($"/api/jobpostings/{posting.Id}/publish", null);
     var applyRes = await Anonymous().PostAsJsonAsync($"/api/public/jobs/{posting.PublicToken}/apply", ...);
     ```
   - Publishing a posting generates a `PublicToken` on the returned DTO. However, `posting` was populated prior to publication when `PublicToken` was `null`.
   - As a result, the request URI resolves to `/api/public/jobs//apply` or `/api/public/jobs/null/apply`, returning a `401 Unauthorized` / `404 Not Found` response when `applyRes.EnsureSuccessStatusCode()` is called at line 83.
   - This invalidates 4 out of 8 tests in `ResumeExtractionTests.cs`.

2. **Regex Defect in Phone Number Parsing (`DocumentTextExtractor.cs`)**:
   - In `DocumentTextExtractor.cs` line 20:
     ```csharp
     private static readonly Regex PhoneRegex = new(
         @"(?:\+?95[-.\s]?|0)?9[-.\s]?\d{1,4}[-.\s]?\d{2,4}[-.\s]?\d{2,4}|(?:\+\d{1,3}[-.\s]?)?\(?\d{1,4}\)?[-.\s]?\d{2,4}[-.\s]?\d{3,4}", RegexOptions.Compiled);
     ```
   - Input `"+95 9 1234 5678"` is tokenized by the regex as `+95` (country code), `9` (mobile prefix), `1234` (4 digits), `567` (3 digits), leaving trailing single digit `8`. Since `\d{2,4}` and `\d{3,4}` require at least 2 or 3 digits at the tail, the regex fails completely and returns `null`.

3. **Regex Defect in Skill Extraction (`DocumentTextExtractor.cs`)**:
   - In `DocumentTextExtractor.cs` lines 205-207:
     ```csharp
     var foundSkills = SkillKeywords
         .Where(k => Regex.IsMatch(text, $@"\b{Regex.Escape(k)}\b", RegexOptions.IgnoreCase))
     ```
   - `SkillKeywords` includes `"C#"` and `".NET"`. In regular expressions, `\b` asserts a word boundary (`\w` to `\W`).
   - `#` and `.` are non-word characters (`\W`). `\bC#\b` requires a word boundary AFTER `#`, which fails when `#` is followed by whitespace, comma, or end-of-string. `\b.NET\b` requires a word boundary BEFORE `.`, which fails when `.` is preceded by whitespace.

4. **Weak Assertions for Preserved File Content in Storage**:
   - In `ResumeExtractionTests.cs` line 257:
     ```csharp
     Assert.NotEmpty(downloadedBytes);
     ```
   - The test only asserts that downloaded bytes are non-empty. It does not verify byte-for-byte identity with the originally uploaded document payload.

5. **Incomplete Edge Case Coverage**:
   - **File Size Boundary**: `UploadResume_FileExceeding10MB_Returns400BadRequest` tests 11MB (`11 * 1024 * 1024` bytes). It does not test the exact 10MB limit boundary (`10 * 1024 * 1024` bytes = 10,485,760 bytes vs 10,485,761 bytes) or 0-byte file uploads.
   - **Format Validation**: Only `.docx`, `.png`, and `.exe` are exercised in integration tests. `.pdf`, `.jpg`, `.jpeg` uploads, as well as uppercase extension formats (`.PDF`, `.DOCX`), lack integration test coverage.

---

## 3. Caveats

- Unit test files `MyanmarScriptNormalizerTests.cs` and `S3FileStorageTests.cs` pass independently when run in isolation.
- No performance or memory stress testing was conducted for multi-gigabyte upload attempts beyond the application controller level.

---

## 4. Conclusion

**Verdict**: `REQUEST_CHANGES`

Milestone 1 does NOT pass backend test execution, and multiple tests in `ResumeExtractionTests.cs` are failing due to a test fixture setup bug (`posting.PublicToken` being null) and regex algorithm defects in `DocumentTextExtractor.cs`. Furthermore, edge case test coverage is incomplete for exact 10MB file boundary enforcement, format validation matrices, and byte-level storage content preservation assertions.

---

## 5. Verification Method

To independently verify these findings:

1. **Run full backend solution tests**:
   ```powershell
   dotnet test backend/RecruitOps.sln
   ```
   *Expected result*: 5 tests fail in `RecruitOps.Api.Tests.ResumeExtractionTests`.

2. **Inspect test implementation**:
   - Inspect `backend/tests/RecruitOps.Api.Tests/ResumeExtractionTests.cs` lines 60-85 to verify that `posting.PublicToken` is referenced prior to updating `posting` with the response of the `publish` endpoint.
   - Inspect `backend/tests/RecruitOps.Api.Tests/ResumeExtractionTests.cs` line 257 to observe the weak `Assert.NotEmpty` assertion.

3. **Inspect extractor implementation**:
   - Inspect `backend/src/Infrastructure/Services/DocumentExtraction/DocumentTextExtractor.cs` line 20 (`PhoneRegex`) and lines 205-207 (skill `\b` word boundary matching).
