# Challenge Report: Milestone 2 — Myanmar Script Normalization (Requirement R2)

**Evaluator:** teamwork_preview_challenger_m2_1 (Empirical Challenger)  
**Date:** 2026-08-07  
**Verdict:** ❌ **REQUEST_CHANGES**  
**Overall Risk Assessment:** **CRITICAL**  

---

## Executive Summary

Empirical testing of `MyanmarScriptNormalizer` revealed a **CRITICAL data corruption flaw** and **false-positive detection bug**. Valid standard Unicode Myanmar text containing common Burmese words (e.g., `သစ်သား`, `စစ်ကိုင်း`, `အသစ်ပြောင်း`, `တစ်နှစ်`) is misdetected as Zawgyi encoding (with confidence up to 0.67) and subsequently corrupted during normalization by replacing legitimate Unicode Asat characters (`\u103A`) with subjoined Virama stackers (`\u1039`).

---

## Empirical Test Results Summary

| Scenario / Test Case | Input | Expected Output | Actual Output | Result |
|----------------------|-------|-----------------|---------------|--------|
| **Valid Unicode Asat (Tree/Wood)** | `"သစ်သား"` (`\u101E\u1005\u103A\u101E\u102C\u1038`) | `IsZawgyi = false`, Text: `"သစ်သား"` | `IsZawgyi = true` (Conf: 0.67), Text: `"သစ္သား"` (Corrupted) | ❌ **FAIL** |
| **Valid Unicode Asat (Sagaing)** | `"စစ်ကိုင်း"` (`\u1005\u1005\u103A\u1000\u102D\u102F\u1004\u103A\u1038`) | `IsZawgyi = false`, Text: `"စစ်ကိုင်း"` | `IsZawgyi = true` (Conf: 0.44), Text: `"စစ္ကိုင်း"` (Corrupted) | ❌ **FAIL** |
| **Valid Unicode Asat (Newly changed)** | `"အသစ်ပြောင်း"` | `IsZawgyi = false`, Text: `"အသစ်ပြောင်း"` | `IsZawgyi = true` (Conf: 0.33), Text: `"အသစ္ပြောင်း"` (Corrupted) | ❌ **FAIL** |
| **Null / Empty / Whitespace** | `null`, `""`, `"   "`, `"\t\n"` | `IsZawgyi = false`, Unchanged | `IsZawgyi = false`, Unchanged | ✅ **PASS** |
| **NFC vs NFD Normalization** | Pure Unicode `မင်္ဂလာပါ` in NFD | `IsZawgyi = false`, FormC NFC output | `IsZawgyi = false`, FormC NFC output | ✅ **PASS** |
| **Mixed English & Zawgyi** | `"Applicant: John Doe, Greetings: မ\u1062လာပါ"` | `IsZawgyi = true`, `"Applicant: John Doe, Greetings: မင်္ဂလာပါ"` | `IsZawgyi = true`, `"Applicant: John Doe, Greetings: မ္ဂလာပါ"` | ❌ **FAIL** |

---

## Detailed Findings & Critical Flaws

### 1. [CRITICAL] False Positive Zawgyi Detection on Valid Unicode Asat Sequences
- **Location**: `backend/src/Infrastructure/Services/MyanmarScript/MyanmarScriptNormalizer.cs` line 20
- **Flaw**: The `ZawgyiExclusiveRegex` includes the pattern:
  ```csharp
  @"|[\u1000-\u1021]\u103A[\u1000-\u1021]"
  ```
- **Explanation**: In Unicode Myanmar script, `[\u1000-\u1021]` matches any Myanmar consonant (Ka `U+1000` to A `U+1021`), and `\u103A` is the Myanmar Sign Asat (`်`). A consonant followed by Asat followed by another consonant (`Consonant + Asat + Consonant`) is a standard, ubiquitous syllable structure in Unicode Burmese for final/killed consonants at word boundaries (e.g., `စစ်` + `ကိုင်း`, `သစ်` + `သား`, `တစ်` + `နှစ်`, `ဖြစ်` + `သည်`).
- **Impact**: Any valid Unicode text containing common Burmese vocabulary is flagged as `IsZawgyi = true`.

### 2. [CRITICAL] Data Corruption of Valid Unicode Text During Normalization
- **Location**: `backend/src/Infrastructure/Services/MyanmarScript/MyanmarScriptNormalizer.cs` line 87
- **Flaw**: Step 2 (`SubjoinedRules`) includes the replacement rule:
  ```csharp
  (new Regex(@"([\u1000-\u1021])\u103A([\u1000-\u1021])", RegexOptions.Compiled), "$1\u1039$2")
  ```
- **Explanation**: When `IsZawgyi` returns `true` (triggered by finding `Consonant + Asat + Consonant`), `ConvertZawgyiToUnicode` executes and unconditionally replaces `Consonant1 + Asat + Consonant2` (`$1\u103A$2`) with `Consonant1 + Virama + Consonant2` (`$1\u1039$2`).
- **Impact**: Legitimate Unicode Asat characters are overwritten with Virama subjoined stackers, rendering valid Unicode text corrupted and ungrammatical (e.g. `သစ်သား` becomes `သစ္သား`).

---

## Recommendations & Mitigations for Worker

1. **Remove False Positive Pattern from Detection**:
   Remove `|[\u1000-\u1021]\u103A[\u1000-\u1021]` from `ZawgyiExclusiveRegex`. In Unicode, `Consonant + Asat + Consonant` is completely valid.

2. **Refine Subjoined Rule Conversion**:
   Do NOT unconditionally convert `Consonant + Asat + Consonant` to `Consonant + Virama + Consonant`. In Zawgyi, subjoined consonants are represented by specific reserved code points (`\u1060`..`\u1069`, `\u106C`..`\u1074`, `\u1076`..`\u107C`, `\u107E`..`\u1084`), which are already converted in lines 50–84 of `SubjoinedRules`.

3. **Verify Standard Zawgyi Converters**:
   Refer to standard open-source Zawgyi-Unicode converters (such as Rabbit Converter or Parabaik) for accurate detection threshold rules and transformation patterns.

4. **Expand Unit Tests**:
   Integrate the test suite in `MyanmarScriptNormalizerChallengerTests.cs` into `MyanmarScriptNormalizerTests.cs` to prevent regressions.
