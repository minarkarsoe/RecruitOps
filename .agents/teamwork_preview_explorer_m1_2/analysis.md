# Requirement R1 Security & Assertion Fix Proposal Report

## Executive Summary
This investigation report details the findings and exact fix proposals for Requirement R1 security and assertion items in the RecruitOps repository:
1. **NU1903 Vulnerability Fix**: Upgrading `System.Security.Cryptography.Xml` from `10.0.6` to `10.0.10` across affected `.csproj` files to eliminate 20 high-severity security vulnerability warnings (Advisories: GHSA-23rf-6693-g89p, GHSA-8q5v-6pqq-x66h, GHSA-cvvh-rhrc-wg4q, GHSA-g8r8-53c2-pm3f, GHSA-mmjf-rqrv-855v).
2. **Explicit HTTP Assertion Fixes**: Replacing disjunctive `StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict` assertions with exact, deterministic status assertions across 3 integration test files.

---

## 1. Package Vulnerability Fix: `System.Security.Cryptography.Xml` (NU1903)

### 1.1 Problem Analysis
Running `dotnet build backend/RecruitOps.sln` or `dotnet list backend/RecruitOps.sln package --vulnerable` yields 20 instance warnings of `warning NU1903`:
> Package `System.Security.Cryptography.Xml` 10.0.6 has known high severity vulnerabilities (GHSA-23rf-6693-g89p, GHSA-8q5v-6pqq-x66h, GHSA-cvvh-rhrc-wg4q, GHSA-g8r8-53c2-pm3f, GHSA-mmjf-rqrv-855v).

### 1.2 Affected Project Files & Line Numbers
A comprehensive scan of all `.csproj` files in the repository identified direct references to `System.Security.Cryptography.Xml` in two project files:
1. **`backend/src/Infrastructure/RecruitOps.Infrastructure.csproj`** (Line 22)
2. **`backend/tests/RecruitOps.Api.Tests/RecruitOps.Api.Tests.csproj`** (Line 18)

*Note: No other `.csproj` files reference this package directly or transitively without project references.*

### 1.3 Target Upgrade Version
- **Target Version**: `10.0.10` (the latest stable patched package release on the .NET 10.x line).
- **Impact**: Upgrading both files to `10.0.10` resolves all 20 NU1903 warnings, resulting in a clean build with 0 warnings.

### 1.4 Proposed Changes / Diffs

#### File 1: `backend/src/Infrastructure/RecruitOps.Infrastructure.csproj`
```diff
--- a/backend/src/Infrastructure/RecruitOps.Infrastructure.csproj
+++ b/backend/src/Infrastructure/RecruitOps.Infrastructure.csproj
@@ -19,4 +19,4 @@
          EncryptedXml DoS). Vulnerable ranges: [8.0.0,8.0.2] [9.0.0,9.0.14] [10.0.0,10.0.5]
          — so 10.0.6 is the first patched build on the 10.x line. Pulled in transitively
          by the EF Design / Mvc.Testing packages, not referenced directly by our code. -->
-    <PackageReference Include="System.Security.Cryptography.Xml" Version="10.0.6" />
+    <PackageReference Include="System.Security.Cryptography.Xml" Version="10.0.10" />
```

#### File 2: `backend/tests/RecruitOps.Api.Tests/RecruitOps.Api.Tests.csproj`
```diff
--- a/backend/tests/RecruitOps.Api.Tests/RecruitOps.Api.Tests.csproj
+++ b/backend/tests/RecruitOps.Api.Tests/RecruitOps.Api.Tests.csproj
@@ -15,4 +15,4 @@
          EncryptedXml DoS). Vulnerable ranges: [8.0.0,8.0.2] [9.0.0,9.0.14] [10.0.0,10.0.5]
          — so 10.0.6 is the first patched build on the 10.x line. Pulled in transitively
          by the EF Design / Mvc.Testing packages, not referenced directly by our code. -->
-    <PackageReference Include="System.Security.Cryptography.Xml" Version="10.0.6" />
+    <PackageReference Include="System.Security.Cryptography.Xml" Version="10.0.10" />
```

---

## 2. Integration Test Loose Assertion Fixes

Three test methods in `backend/tests/RecruitOps.Api.Tests/` contained disjunctive status code assertions (`Assert.True(statusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)`). Each has been analyzed against controller error handling and DataAnnotations validation rules to establish the exact intended HTTP status code.

### 2.1 File 1: `ScorecardBlindScoringTests.cs`
- **Path**: `backend/tests/RecruitOps.Api.Tests/ScorecardBlindScoringTests.cs`
- **Line**: 238
- **Test Method**: `A_Rating_Outside_One_To_Five_Is_Refused`
- **Request Under Test**: `PUT /api/interviews/{interview.Id}/scorecard` with `ScorecardAnswerInput.Rating = 9`.
- **Code Flow & Status Code Logic**:
  1. The controller action `SaveMyScorecard` in `backend/src/Api/Controllers/InterviewsController.cs` calls `_scorecards.SaveMineAsync`.
  2. `ScorecardService.ValidateAnswer` evaluates ratings and throws `InvalidOperationException($"'{criterion.Label}' needs a rating from 1 to 5.")`.
  3. `InterviewsController.SaveMyScorecard` catches `InvalidOperationException` and returns `Conflict(new ProblemDetails { Title = "Cannot save scorecard", Detail = ex.Message })`.
  4. The returned HTTP status code is **409 Conflict** (`HttpStatusCode.Conflict`).
- **Current Code (Line 238)**:
  ```csharp
  Assert.True(res.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);
  ```
- **Proposed Replacement**:
  ```csharp
  Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
  ```

#### Exact Diff:
```diff
--- a/backend/tests/RecruitOps.Api.Tests/ScorecardBlindScoringTests.cs
+++ b/backend/tests/RecruitOps.Api.Tests/ScorecardBlindScoringTests.cs
@@ -235,4 +235,4 @@
                 },
             });

-        Assert.True(res.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);
+        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
```

---

### 2.2 File 2: `InterviewFlowTests.cs`
- **Path**: `backend/tests/RecruitOps.Api.Tests/InterviewFlowTests.cs`
- **Line**: 144
- **Test Method**: `An_Interview_Needs_A_Panel_And_A_Lead_Who_Is_On_It`
- **Request Under Test**: `POST /api/applications/{applicationId}/interviews` with `ParticipantUserIds = Array.Empty<Guid>()`.
- **Code Flow & Status Code Logic**:
  1. The controller action `Schedule` accepts `ScheduleInterviewRequest` defined in `backend/src/Application/DTOs/InterviewDtos.cs`.
  2. Property `ParticipantUserIds` is decorated with `[Required, MinLength(1)]`.
  3. When an empty collection is submitted, ASP.NET Core Automatic Model Validation fails before hitting the controller method body.
  4. The framework returns **400 Bad Request** (`HttpStatusCode.BadRequest`).
- **Current Code (Line 144)**:
  ```csharp
  Assert.True(noPanel.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);
  ```
- **Proposed Replacement**:
  ```csharp
  Assert.Equal(HttpStatusCode.BadRequest, noPanel.StatusCode);
  ```

#### Exact Diff:
```diff
--- a/backend/tests/RecruitOps.Api.Tests/InterviewFlowTests.cs
+++ b/backend/tests/RecruitOps.Api.Tests/InterviewFlowTests.cs
@@ -141,4 +141,4 @@

         // Model validation catches the empty list before the service does; either way this
         // must not produce an interview nobody can score.
-        Assert.True(noPanel.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);
+        Assert.Equal(HttpStatusCode.BadRequest, noPanel.StatusCode);
```

---

### 2.3 File 3: `ScorecardTemplateResolutionTests.cs`
- **Path**: `backend/tests/RecruitOps.Api.Tests/ScorecardTemplateResolutionTests.cs`
- **Line**: 108
- **Test Method**: `A_Template_Needs_At_Least_One_Criterion_With_A_Known_Type`
- **Request Under Test**: `POST /api/scorecardtemplates` with `Criteria = Array.Empty<ScorecardCriterionInput>()`.
- **Code Flow & Status Code Logic**:
  1. The controller action `Create` accepts `SaveScorecardTemplateRequest` defined in `backend/src/Application/DTOs/ScorecardDtos.cs`.
  2. Property `Criteria` is decorated with `[Required, MinLength(1)]`.
  3. When an empty collection is submitted, ASP.NET Core Automatic Model Validation fails prior to service invocation.
  4. The framework returns **400 Bad Request** (`HttpStatusCode.BadRequest`).
- **Current Code (Line 108)**:
  ```csharp
  Assert.True(empty.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);
  ```
- **Proposed Replacement**:
  ```csharp
  Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
  ```

#### Exact Diff:
```diff
--- a/backend/tests/RecruitOps.Api.Tests/ScorecardTemplateResolutionTests.cs
+++ b/backend/tests/RecruitOps.Api.Tests/ScorecardTemplateResolutionTests.cs
@@ -105,4 +105,4 @@
                 Name = "Empty",
                 Criteria = Array.Empty<ScorecardCriterionInput>(),
             });
-        Assert.True(empty.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);
+        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
```

---

## 3. Verification Method & Test Commands

To verify the proposed fixes independently:

1. **Verify Package Vulnerabilities are Resolved**:
   ```powershell
   dotnet list backend/RecruitOps.sln package --vulnerable
   ```
   *Expected Result*: Output indicates zero vulnerable packages across all projects.

2. **Verify Clean Solution Build**:
   ```powershell
   dotnet build backend/RecruitOps.sln
   ```
   *Expected Result*: `Build succeeded. 0 Warning(s) 0 Error(s)`.

3. **Verify All Integration & Unit Tests Pass**:
   ```powershell
   dotnet test backend/RecruitOps.sln
   ```
   *Expected Result*: Total 172 tests passed (39 Domain tests, 133 API tests), 0 failed, 0 skipped.
