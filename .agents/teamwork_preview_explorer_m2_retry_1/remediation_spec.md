# Remediation Specification: Myanmar Script Normalization (Milestone 2 Retry 1)

**Author:** teamwork_preview_explorer (Milestone 2 Retry 1)  
**Date:** 2026-08-07  
**Target File:** `backend/src/Infrastructure/Services/MyanmarScript/MyanmarScriptNormalizer.cs`  
**Related Test File:** `backend/tests/RecruitOps.Api.Tests/MyanmarScriptNormalizerChallengerTests.cs`  
**Status:** SPECIFICATION COMPLETE  

---

## 1. Problem Statement & Context

During Milestone 2 verification, empirical testing by the Challenger role revealed that standard Unicode Burmese strings containing common vocabulary (such as `သစ်သား`, `စစ်ကိုင်း`, `အသစ်ပြောင်း`, `မင်မင်္ဂလာ`) fail normalized output assertions with 5 test failures in `RecruitOps.Api.Tests`:

- **False-Positive Zawgyi Detection:** Valid Unicode strings are misidentified as `IsZawgyiDetected = true` with confidence scores ranging from 0.33 to 0.67.
- **Data Corruption:** Once falsely flagged as Zawgyi, the normalizer executes `ConvertZawgyiToUnicode`, replacing legitimate Unicode Asat characters (`\u103A`) with subjoined Virama stackers (`\u1039`), rendering valid Burmese text ungrammatical and corrupted (e.g., `သစ်သား` becomes `သစ္သား`).

---

## 2. Technical Analysis & Forensic Root Cause

### 2.1 The Role of Asat (`\u103A`) vs Virama (`\u1039`) in Unicode Burmese
- **Asat (`\u103A` / `်`):** Represents a killed consonant (final consonant in a syllable). In standard Unicode Burmese, when a syllable ending with a killed consonant is followed by a starting consonant of the next syllable (e.g. `သစ်` + `သား` = `သစ်သား`), the canonical representation is **Consonant + Asat (`\u103A`) + Consonant**.
- **Virama (`\u1039`):** Represents a non-printing subjoined consonant stacker. It must be followed by a consonant to render a stacked subscript consonant (e.g., `မ` + `င` + `\u1039` + `ဂ` = `မင်္ဂ`).

### 2.2 Forensic Flaws in `MyanmarScriptNormalizer.cs`

1. **Flaw 1: False Pattern in Detection Regex (`ZawgyiExclusiveRegex` line 20)**
   ```csharp
   // Line 20 in ZawgyiExclusiveRegex:
   @"|[\u1000-\u1021]\u103A[\u1000-\u1021]"
   ```
   `[\u1000-\u1021]` matches any Myanmar consonant (`က` through `အ`). Matching `Consonant + Asat (\u103A) + Consonant` as a "Zawgyi-exclusive" feature is false, because this sequence is canonical standard Unicode for syllable boundaries. This single regex line causes every Burmese text containing common killed-consonant vocabulary to be misclassified as Zawgyi.

2. **Flaw 2: Corrupting Rule in Subjoined Conversion (`SubjoinedRules` line 87)**
   ```csharp
   // Line 87 in SubjoinedRules:
   (new Regex(@"([\u1000-\u1021])\u103A([\u1000-\u1021])", RegexOptions.Compiled), "$1\u1039$2")
   ```
   When `IsZawgyiDetected` evaluates to `true`, `ConvertZawgyiToUnicode` runs `SubjoinedRules`. Line 87 unconditionally overwrites `Consonant + Asat + Consonant` (`$1\u103A$2`) with `Consonant + Virama + Consonant` (`$1\u1039$2`), turning legitimate Asat characters into stacked Virama consonants.

3. **Flaw 3: Incomplete Mapping for Zawgyi Subjoined Ga / Kinzi (`\u1062`)**
   In `SubjoinedRules` line 52:
   ```csharp
   (new Regex(@"\u1062", RegexOptions.Compiled), "\u1039\u1002")
   ```
   When Zawgyi text contains `\u1062` representing Kinzi `င်္ဂ` without an explicit `\u1004` preceding it (e.g., `မ\u1062လာပါ`), replacing `\u1062` with only `\u1039\u1002` produces `မ္ဂလာပါ` (missing the Nga `\u1004`).

---

## 3. Remediation Strategy & Exact Code Edits

### Edit 1: Update `ZawgyiExclusiveRegex` in `MyanmarScriptNormalizer.cs`
Remove `|[\u1000-\u1021]\u103A[\u1000-\u1021]` from `ZawgyiExclusiveRegex` completely.

**Target File:** `backend/src/Infrastructure/Services/MyanmarScript/MyanmarScriptNormalizer.cs`  
**Line Range:** 15–24

```csharp
<<<< BEFORE
    private static readonly Regex ZawgyiExclusiveRegex = new(
        @"[\u1060-\u1069\u106C-\u1070\u1071-\u108F\u1090-\u1097]" +
        @"|\u1031[\u1000-\u1021]" +
        @"|\u1031[\u107D-\u1084\u103C][\u1000-\u1021]" +
        @"|\u103C[\u1000-\u1021]" +
        @"|[\u1000-\u1021]\u103A[\u1000-\u1021]" +
        @"|\u102D\u1032" +
        @"|\u103A\u1037" +
        @"|\u1039(?![\u1000-\u1021])",
        RegexOptions.Compiled);
==== AFTER
    private static readonly Regex ZawgyiExclusiveRegex = new(
        @"[\u1060-\u1069\u106C-\u1070\u1071-\u108F\u1090-\u1097]" +
        @"|\u1031[\u1000-\u1021]" +
        @"|\u1031[\u107D-\u1084\u103C][\u1000-\u1021]" +
        @"|\u103C[\u1000-\u1021]" +
        @"|\u102D\u1032" +
        @"|\u103A\u1037" +
        @"|\u1039(?![\u1000-\u1021])",
        RegexOptions.Compiled);
>>>>
```

### Edit 2: Update `SubjoinedRules` in `MyanmarScriptNormalizer.cs`
1. Remove line 87 (`([\u1000-\u1021])\u103A([\u1000-\u1021])` -> `$1\u1039$2`).
2. Add rule for `\u1004\u1062` -> `\u1004\u1039\u1002` and update `\u1062` -> `\u1004\u1039\u1002`.

**Target File:** `backend/src/Infrastructure/Services/MyanmarScript/MyanmarScriptNormalizer.cs`  
**Line Range:** 48–88

```csharp
<<<< BEFORE
    private static readonly (Regex Pattern, string Replacement)[] SubjoinedRules = new[]
    {
        (new Regex(@"\u1060", RegexOptions.Compiled), "\u1039\u1000"), // Subjoined Ka
        (new Regex(@"\u1061", RegexOptions.Compiled), "\u1039\u1001"), // Subjoined Kha
        (new Regex(@"\u1062", RegexOptions.Compiled), "\u1039\u1002"), // Subjoined Ga
        (new Regex(@"\u1063", RegexOptions.Compiled), "\u1039\u1003"), // Subjoined Gha
...
        (new Regex(@"\u1084", RegexOptions.Compiled), "\u1039\u1019"), // Subjoined Ma

        // Convert Zawgyi Asat between consonants to Unicode Virama subjoined consonant
        (new Regex(@"([\u1000-\u1021])\u103A([\u1000-\u1021])", RegexOptions.Compiled), "$1\u1039$2"),
    };
==== AFTER
    private static readonly (Regex Pattern, string Replacement)[] SubjoinedRules = new[]
    {
        (new Regex(@"\u1060", RegexOptions.Compiled), "\u1039\u1000"), // Subjoined Ka
        (new Regex(@"\u1061", RegexOptions.Compiled), "\u1039\u1001"), // Subjoined Kha
        (new Regex(@"\u1004\u1062", RegexOptions.Compiled), "\u1004\u1039\u1002"), // Nga + Zawgyi subjoined Ga -> Unicode Kinzi Ga (င်္ဂ)
        (new Regex(@"\u1062", RegexOptions.Compiled), "\u1004\u1039\u1002"),     // Standalone Zawgyi Kinzi/subjoined Ga -> Unicode Kinzi Ga (င်္ဂ)
        (new Regex(@"\u1063", RegexOptions.Compiled), "\u1039\u1003"), // Subjoined Gha
...
        (new Regex(@"\u1084", RegexOptions.Compiled), "\u1039\u1019"), // Subjoined Ma
    };
>>>>
```

---

## 4. Preservation of True Zawgyi Detection

The proposed fix preserves all valid Zawgyi detection features in `ZawgyiExclusiveRegex`:

1. **Reserved Zawgyi Codepoints (`[\u1060-\u1069\u106C-\u1070\u1071-\u108F\u1090-\u1097]`):**
   Matches Zawgyi-specific glyph positions (e.g. `\u1062`, `\u107E`, `\u1080`, `\u1088`, `\u1097`), which are invalid in Unicode standard text.
2. **Visual E-Vowel Reordering (`\u1031[\u1000-\u1021]` and `\u1031[\u107D-\u1084\u103C][\u1000-\u1021]`):**
   Matches `U+1031` placed before consonants/medials, which never occurs in Unicode logical ordering.
3. **Preceding Ra-gyit (`\u103C[\u1000-\u1021]`):**
   Matches `U+103C` placed before consonants.
4. **Invalid Diacritic Combinations (`\u102D\u1032`, `\u103A\u1037`):**
   Matches Zawgyi diacritic sequences.
5. **Dangling Virama (`\u1039(?![\u1000-\u1021])`):**
   Matches Virama not followed by a consonant.

---

## 5. Verification Plan

1. **Run Full Backend Test Suite:**
   ```powershell
   dotnet test backend/RecruitOps.sln
   ```
   - **Expected Outcome:** 327/327 tests pass (51/51 Domain + 276/276 Api). 0 test failures.

2. **Specific Test Executions:**
   - `MyanmarScriptNormalizerTests.cs` (7 tests pass)
   - `MyanmarScriptNormalizerChallengerTests.cs` (5 tests pass, including all valid Unicode Asat words and mixed Zawgyi/English)
   - `MyanmarScriptNormalizerStressTests.cs` (Performance & thread safety maintained)

3. **Verification Command Summary:**
   - Execute `dotnet test backend/RecruitOps.sln` and ensure exit code 0.
