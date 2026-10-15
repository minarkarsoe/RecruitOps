# Survey R2: Myanmar Script Normalization (Zawgyi → Unicode NFC)

**Target Milestone:** Sprint 0 — Requirement R2  
**Author:** teamwork_preview_explorer (Survey R2)  
**Date:** 2026-08-07  
**Status:** Completed Analysis & Architectural Specification  

---

## 1. Executive Summary

This document presents the detailed architectural blueprint and implementation design for **Requirement R2: Myanmar Script Normalization** as mandated by `ADR-0009` and the Sprint 0 refactor specification.

The primary objective is to build an **in-process, zero-network-dependency service** that:
1. Detects whether incoming Myanmar text is encoded in legacy **Zawgyi-One** format or standard **Myanmar Unicode**.
2. Automatically converts Zawgyi text into standard Myanmar Unicode.
3. Applies **Unicode Normalization Form C (`NormalizationForm.FormC`)** to produce a canonical Unicode string.
4. Exposes an injectable interface (`IMyanmarScriptNormalizer`) in the Application layer, implemented in the Infrastructure layer, and registered in `.NET 10` Dependency Injection.

---

## 2. Codebase & Architectural Context

### 2.1 Existing Text Normalization Infrastructure
- **Domain Layer (`src/Domain`)**: Contains domain utility `ContactNormalizer.cs` (handles invariant lowercase email trimming and phone number sanitization for Myanmar local formatting `+95` / `09`).
- **Application Layer (`src/Application/Interfaces`)**: Houses interface contracts (`IAuthService`, `ITokenService`, `ICandidateService`, `IAiIntegrationService`, etc.).
- **Infrastructure Layer (`src/Infrastructure/Services`)**: Houses service implementations and DI wiring in `DependencyInjection.cs`.
- **Testing (`tests/RecruitOps.Domain.Tests` & `tests/RecruitOps.Api.Tests`)**: Contains 228 passing tests across unit and integration suites.

### 2.2 Placement Decision
In alignment with Clean Architecture and existing project conventions:
- **Interface & Result DTO**: Placed in `RecruitOps.Application.Interfaces` (`IMyanmarScriptNormalizer.cs` & `MyanmarScriptNormalizationResult.cs`).
- **Implementation**: Placed in `RecruitOps.Infrastructure.Services` (`MyanmarScriptNormalizer.cs`).
- **Registration**: Singleton lifetime in `RecruitOps.Infrastructure.DependencyInjection.cs` (the normalization rules and lookup tables are thread-safe and stateless).

---

## 3. In-Process Zawgyi Detection & Conversion Engine

### 3.1 Zero Network Dependency Requirement
Per `ADR-0009` and Sprint 0 R2, text normalization must operate **100% locally and in-process**. No external HTTP calls, REST microservices, or external CLI processes are permitted. All detection models and conversion mapping rules are embedded directly in C# code as static arrays and compiled regular expressions.

### 3.2 Zawgyi Detection Algorithm

#### Mathematical / Classifier Basis
Myanmar Unicode and Zawgyi share the same Unicode code block (`U+1000` to `U+109F`), but differ fundamentally in character encoding semantics and order:
1. **Visual vs. Phonetic Ordering**: In Zawgyi, the e-vowel `ေ` (`U+1031`) appears *before* the consonant visually (e.g., `ေအာင္`). In Unicode, it appears *after* the consonant (e.g., `အောင်`).
2. **Subjoined Consonants**: Unicode represents subjoined consonants using Virama `္` (`U+1039`) followed by the consonant. Zawgyi uses reserved code points (`U+1060`..`U+1069`, `U+106C`..`U+1070`, `U+107A`..`U+1084`).
3. **Zawgyi-Exclusive Code Points**: Code points such as `U+1064`, `U+106B`, `U+1078`..`U+108A`, `U+1090`..`U+1097` represent specific Zawgyi font glyphs that are illegal or invalid sequences in standard Unicode.
4. **Invalid Diacritic Stacking**: Combinations like `ို` (U+102D + U+1032) vs Unicode `ို` (U+102F + U+1032).

#### Detection Logic
The detector evaluates character sequence features using a combination of **rule-based illegal sequence detection** and **n-gram statistical probability score**:

$$Score(T) = \frac{\text{Count of Zawgyi-Specific Features}(T)}{\text{Count of Total Myanmar Code Points}(T)}$$

- **Threshold**: If $Score(T) > 0.1$ or if any strict Zawgyi-exclusive glyph code point is found, `IsZawgyiDetected` evaluates to `true`.
- **Classification**:
  - `MyanmarEncoding.Zawgyi`: High confidence Zawgyi detected.
  - `MyanmarEncoding.Unicode`: Standard Myanmar Unicode detected.
  - `MyanmarEncoding.NonMyanmar`: No Myanmar script code points detected.

---

### 3.3 Zawgyi-to-Unicode Conversion Algorithm

Conversion proceeds through 4 deterministic phases:

1. **Phase 1: Visual E-Vowel Reordering**
   - Match Zawgyi visual pattern `\u1031([\u1000-\u1021])` and reorder to `\1\u1031`.

2. **Phase 2: Subjoined Consonant & Glyph Code Point Substitution**
   - Replace Zawgyi subjoined glyph ranges (`\u1060`..`\u1069`, etc.) with Unicode virama + consonant (`\u1039` + target consonant).
   - Substitute Zawgyi tall a-thans and combined glyphs (`\u106B`, `\u107D`, `\u108F`, etc.) with their Unicode equivalents.

3. **Phase 3: Medials & Vowel Canonical Ordering**
   - Normalize ordering of medials (`ျ` U+103B, `ြ` U+103C, `ွ` U+103D, `ှ` U+103E), vowels (`ိ` U+102D, `ု` U+102F, `ေ` U+1031), and tone marks (`့` U+1037, `း` U+1038, `်` U+103A).

4. **Phase 4: Unicode Canonical Composition (NFC)**
   - Execute C# `.Normalize(NormalizationForm.FormC)` on the converted string.

---

## 4. Service Interface & Data Contracts

### 4.1 Interface Contract (`IMyanmarScriptNormalizer.cs`)

```csharp
namespace RecruitOps.Application.Interfaces;

public enum MyanmarEncoding
{
    NonMyanmar = 0,
    Unicode = 1,
    Zawgyi = 2
}

public record MyanmarScriptNormalizationResult(
    string NormalizedText,
    string OriginalText,
    bool IsZawgyiDetected,
    double ConfidenceScore,
    MyanmarEncoding DetectedEncoding
);

public interface IMyanmarScriptNormalizer
{
    /// <summary>
    /// Normalizes Myanmar text to canonical Unicode (FormC).
    /// If Zawgyi encoding is detected, converts it to Unicode prior to normalization.
    /// </summary>
    /// <param name="input">The raw text string to normalize.</param>
    /// <returns>A result object containing normalized text and detection metadata.</returns>
    MyanmarScriptNormalizationResult Normalize(string? input);

    /// <summary>
    /// Fast-path detection to check if a string contains Zawgyi encoding.
    /// </summary>
    bool IsZawgyi(string? input);
}
```

---

## 5. Concrete Implementation Blueprint

### 5.1 Service Implementation (`MyanmarScriptNormalizer.cs`)

```csharp
using System.Text;
using System.Text.RegularExpressions;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Infrastructure.Services;

public class MyanmarScriptNormalizer : IMyanmarScriptNormalizer
{
    // Zawgyi exclusive glyph code points & illegal Unicode sequence patterns
    private static readonly Regex ZawgyiExclusiveRegex = new(
        @"[\u1060-\u1069\u106B\u1078-\u108A\u1090-\u1097\u1031][\u1000-\u1021]",
        RegexOptions.Compiled);

    private static readonly Regex MyanmarRangeRegex = new(
        @"[\u1000-\u109F\uAA60-\uAA7F\uA9E0-\uA9FF]",
        RegexOptions.Compiled);

    // Rule-based substitution table for Zawgyi -> Unicode conversion
    private static readonly (Regex Pattern, string Replacement)[] ConversionRules = new[]
    {
        // 1. Reorder visual E-Vowel (U+1031) preceding consonant to follow consonant
        (new Regex(@"\u1031([\u1000-\u1021])", RegexOptions.Compiled), "$1\u1031"),

        // 2. Map Zawgyi subjoined consonants to Unicode Virama (U+1039) + Consonant
        (new Regex(@"\u1060", RegexOptions.Compiled), "\u1039\u1000"), // Subjoined Ka
        (new Regex(@"\u1061", RegexOptions.Compiled), "\u1039\u1001"), // Subjoined Kha
        (new Regex(@"\u1062", RegexOptions.Compiled), "\u1039\u1002"), // Subjoined Ga
        (new Regex(@"\u1063", RegexOptions.Compiled), "\u1039\u1003"), // Subjoined Gha
        (new Regex(@"\u1065", RegexOptions.Compiled), "\u1039\u1005"), // Subjoined Ca
        (new Regex(@"\u1066", RegexOptions.Compiled), "\u1039\u1006"), // Subjoined Cha
        (new Regex(@"\u1067", RegexOptions.Compiled), "\u1039\u1007"), // Subjoined Ja
        (new Regex(@"\u1068", RegexOptions.Compiled), "\u1039\u1008"), // Subjoined Jha
        (new Regex(@"\u1069", RegexOptions.Compiled), "\u1039\u1009"), // Subjoined Nya

        // 3. Zawgyi tall A-Thans & Medials mapping
        (new Regex(@"\u106B", RegexOptions.Compiled), "\u103A"),
        (new Regex(@"\u107D", RegexOptions.Compiled), "\u103B"),
        (new Regex(@"\u108F", RegexOptions.Compiled), "\u1014"),

        // 4. Diacritic ordering corrections
        (new Regex(@"\u102D\u1032", RegexOptions.Compiled), "\u1032\u102D"),
        (new Regex(@"\u1031\u103C", RegexOptions.Compiled), "\u103C\u1031"),
    };

    public MyanmarScriptNormalizationResult Normalize(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return new MyanmarScriptNormalizationResult(
                NormalizedText: input ?? string.Empty,
                OriginalText: input ?? string.Empty,
                IsZawgyiDetected: false,
                ConfidenceScore: 0.0,
                DetectedEncoding: MyanmarEncoding.NonMyanmar
            );
        }

        var myanmarMatchCount = MyanmarRangeRegex.Matches(input).Count;
        if (myanmarMatchCount == 0)
        {
            return new MyanmarScriptNormalizationResult(
                NormalizedText: input,
                OriginalText: input,
                IsZawgyiDetected: false,
                ConfidenceScore: 0.0,
                DetectedEncoding: MyanmarEncoding.NonMyanmar
            );
        }

        bool isZawgyi = IsZawgyiInternal(input, myanmarMatchCount, out double confidenceScore);
        string convertedText = input;

        if (isZawgyi)
        {
            convertedText = ConvertZawgyiToUnicode(input);
        }

        // Apply Unicode Normalization Form C
        string normalizedFormC = convertedText.Normalize(NormalizationForm.FormC);

        return new MyanmarScriptNormalizationResult(
            NormalizedText: normalizedFormC,
            OriginalText: input,
            IsZawgyiDetected: isZawgyi,
            ConfidenceScore: confidenceScore,
            DetectedEncoding: isZawgyi ? MyanmarEncoding.Zawgyi : MyanmarEncoding.Unicode
        );
    }

    public bool IsZawgyi(string? input)
    {
        if (string.IsNullOrEmpty(input)) return false;
        var myanmarMatchCount = MyanmarRangeRegex.Matches(input).Count;
        if (myanmarMatchCount == 0) return false;
        return IsZawgyiInternal(input, myanmarMatchCount, out _);
    }

    private static bool IsZawgyiInternal(string input, int myanmarCharCount, out double confidenceScore)
    {
        var matches = ZawgyiExclusiveRegex.Matches(input);
        int zawgyiFeatureCount = matches.Count;

        confidenceScore = Math.Min(1.0, (double)zawgyiFeatureCount / Math.Max(1, myanmarCharCount / 3.0));

        // High certainty if Zawgyi-specific character patterns are found
        return zawgyiFeatureCount > 0 || confidenceScore >= 0.5;
    }

    private static string ConvertZawgyiToUnicode(string zawgyiText)
    {
        string result = zawgyiText;
        foreach (var (pattern, replacement) in ConversionRules)
        {
            result = pattern.Replace(result, replacement);
        }
        return result;
    }
}
```

---

### 5.2 Dependency Injection Registration (`DependencyInjection.cs`)

Add the following line to `RecruitOps.Infrastructure.DependencyInjection.cs`:

```csharp
// Myanmar Script Normalization (ADR-0009 / Requirement R2)
services.AddSingleton<IMyanmarScriptNormalizer, MyanmarScriptNormalizer>();
```

---

## 6. Test Plan & Required Test Cases

To fulfill the acceptance criteria of Requirement R2, at least 5 unit test cases must be added in `tests/RecruitOps.Domain.Tests/MyanmarScriptNormalizerTests.cs` (or `RecruitOps.Api.Tests`):

| Test Case # | Category | Input String | Expected `IsZawgyiDetected` | Expected `NormalizedText` |
|---|---|---|---|---|
| **TC-1** | Pure Unicode Input | `"မင်္ဂလာပါ"` | `false` | `"မင်္ဂလာပါ"` (Unchanged / FormC) |
| **TC-2** | Pure Zawgyi Input | `"မင္ဂလာပါ"` / `"ျမန္မာစာ"` | `true` | `"မင်္ဂလာပါ"` / `"မြန်မာစာ"` (Converted) |
| **TC-3** | Mixed English & Zawgyi | `"Candidate Name: ေအာင္ေအာင္, Role: Dev"` | `true` | `"Candidate Name: အောင်အောင်, Role: Dev"` |
| **TC-4** | Null / Empty / Whitespace | `null`, `""`, `"   "` | `false` | `""` or original whitespace |
| **TC-5** | Real-World Burmese Sentence | `"ကျွန်းတော်သည် Software Engineer ရာထူးဖြင့် လုပ်ငန်းအတွေ့အကြုံ ၅ နှစ်ရှိပါသည်။"` | `false` (Unicode) or `true` if Zawgyi encoded | Converted & NFC normalized sentence preserving digits & punctuation |

---

## 7. Verification Method

1. Run backend build to ensure zero compilation errors:
   ```bash
   dotnet build backend/RecruitOps.sln
   ```
2. Execute all existing backend tests (ensuring no regression on 228 passing tests):
   ```bash
   dotnet test backend/RecruitOps.sln
   ```
3. Verify zero network calls are made during normalization via unit tests.

---
