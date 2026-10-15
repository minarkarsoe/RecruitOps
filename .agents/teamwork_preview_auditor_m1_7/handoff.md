# Forensic Audit Report — Milestone 1 (CV Resume Storage & Document Extraction Backend API)

**Work Product**: Milestone 1 Backend Implementation (`DocumentTextExtractor.cs`, `ResumeService.cs`, `ApplicationsController.cs`, `ResumeExtractionTests.cs`)
**Profile**: General Project (Development Mode)
**Verdict**: INTEGRITY VIOLATION

---

## 1. Observation

Empirical findings from code analysis and test execution:

1. **Test Suite Execution Failure (`dotnet test backend/tests/RecruitOps.Api.Tests/RecruitOps.Api.Tests.csproj`)**:
   - **Failure 1 (Unit Test Logic Error)**: `RecruitOps.Api.Tests.ResumeExtractionTests.DocumentTextExtractor_ParsesContactInfoHeuristics` FAILED.
     - Line: `ResumeExtractionTests.cs:207`
     - Error Message:
       ```
       Assert.Equal() Failure: Strings differ
       Expected: "+95 9 1234 5678"
       Actual:   null
       ```
     - Root cause: `PhoneRegex` in `DocumentTextExtractor.cs:20` fails to parse international formatted Myanmar phone numbers with spaced grouping such as `"+95 9 1234 5678"`, returning `null`.

   - **Failure 2 (Test Host Crash / Socket Timeout)**: `UploadResume_...` integration tests in `ResumeExtractionTests.cs` (specifically `UploadResume_ZawgyiNormalization_NormalizesToUnicode` and `UploadResume_SuccessfulDocx_Returns200AndExtractedText`) aborted and crashed the test host runner.
     - Error Message:
       ```
       [xUnit.net 00:01:45.70] RecruitOps.Api.Tests.ResumeExtractionTests.UploadResume_ZawgyiNormalization_NormalizesToUnicode [FAIL]
       System.Threading.Tasks.TaskCanceledException : The operation was canceled.
       ---- System.Net.Http.HttpRequestException : Error while copying content to a stream.
       -------- System.IO.IOException : The client aborted the request.
       The active test run was aborted. Reason: Test host process crashed
       ```
     - Root cause: `CustomWebAppFactory.cs` does not register a test double (stub/mock) for `IFileStorage`. `ResumeService.cs` attempts to call `_storage.UploadAsync` which defaults to `S3FileStorage` (`c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\backend\src\Infrastructure\DependencyInjection.cs:97`). Without a local MinIO container active, requests hang for 1m40s and abort, crashing the test runner.

2. **Source Code Implementation Inspection**:
   - `DocumentTextExtractor.cs`: Implements genuine stream parsing using `UglyToad.PdfPig` (PDF) and OpenXML `ZipArchive`/`XDocument` (DOCX). However, its phone regex heuristic is broken for spaced international phone numbers.
   - `ResumeService.cs`: Implements genuine workflow connecting database entities and storage abstractions, but lacks offline fallback handling during automated test runs.
   - `ApplicationsController.cs`: Implements HTTP validation (size limits, extension checks) and routes to services genuinely.

3. **Package Licensing**:
   - `UglyToad.PdfPig` version `1.7.0-custom-5` in `backend/src/Infrastructure/RecruitOps.Infrastructure.csproj` is permissively licensed under Apache License 2.0.

---

## 2. Logic Chain

1. **Integrity Rule**: Per project forensic audit standards, a work product must build cleanly and all tests in the test suite must execute and pass without crashing or failing.
2. **Observation 1**: `DocumentTextExtractor_ParsesContactInfoHeuristics` fails assertions because `PhoneRegex` returns `null` for `+95 9 1234 5678`.
3. **Observation 2**: API upload tests in `ResumeExtractionTests.cs` time out (100s+) and crash the test host process because `CustomWebAppFactory` registers `S3FileStorage` instead of an in-memory/stub `IFileStorage`.
4. **Conclusion**: The work product fails behavioral verification and has broken test execution. The verdict is **INTEGRITY VIOLATION**.

---

## 3. Caveats

- The source code in `DocumentTextExtractor.cs` and `ResumeService.cs` does not contain hardcoded fake outputs or facade classes. The failure is due to broken regex logic in phone parsing and missing test fixture doubles for object storage in `CustomWebAppFactory`.

---

## 4. Conclusion

Milestone 1 contains failing unit tests and crashing integration test suites due to unhandled storage dependencies in `CustomWebAppFactory` and regex parsing bugs in `DocumentTextExtractor.cs`.

**Verdict**: INTEGRITY VIOLATION

---

## 5. Verification Method

To reproduce the test failures independently:

1. Run the failing contact info unit test:
   ```powershell
   dotnet test backend/tests/RecruitOps.Api.Tests/RecruitOps.Api.Tests.csproj --filter "FullyQualifiedName~DocumentTextExtractor_ParsesContactInfoHeuristics"
   ```
   Observe: `Assert.Equal() Failure: Expected: "+95 9 1234 5678", Actual: null`.

2. Run the API upload integration test (without MinIO running):
   ```powershell
   dotnet test backend/tests/RecruitOps.Api.Tests/RecruitOps.Api.Tests.csproj --filter "FullyQualifiedName~UploadResume_SuccessfulDocx_Returns200AndExtractedText"
   ```
   Observe: Test hangs for ~1m40s and aborts with `TaskCanceledException` / Test host process crash.
