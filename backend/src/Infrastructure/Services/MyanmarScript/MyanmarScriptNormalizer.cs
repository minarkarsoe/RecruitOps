using System.Text;
using System.Text.RegularExpressions;
using RecruitOps.Application.Interfaces;

namespace RecruitOps.Infrastructure.Services.MyanmarScript;

public class MyanmarScriptNormalizer : IMyanmarScriptNormalizer
{
    // Regex matching any Myanmar script code points (inclusive of extended ranges)
    private static readonly Regex MyanmarRangeRegex = new(
        @"[\u1000-\u109F\uAA60-\uAA7F\uA9E0-\uA9FF]",
        RegexOptions.Compiled);

    // Regex matching Zawgyi-exclusive code points and illegal Unicode visual order combinations
    private static readonly Regex ZawgyiExclusiveRegex = new(
        @"[\u1060-\u1069\u106C-\u1070\u1071-\u108F\u1090-\u1097]" +
        @"|\u1031[\u1000-\u1021]" +
        @"|\u1031[\u107D-\u1084\u103C][\u1000-\u1021]" +
        @"|\u103C[\u1000-\u1021]" +
        @"|\u102D\u1032" +
        @"|\u103A\u1037" +
        @"|\u1039(?![\u1000-\u1021])",
        RegexOptions.Compiled);

    // Step 1: Pre-substitutions for Zawgyi specific glyphs
    private static readonly (Regex Pattern, string Replacement)[] GlyphPreSubstitutions = new[]
    {
        (new Regex(@"\u106A", RegexOptions.Compiled), "\u1009"),       // Zawgyi Nya -> Unicode Nya
        (new Regex(@"\u106B", RegexOptions.Compiled), "\u103A"),       // Zawgyi Asat -> Unicode Asat
        (new Regex(@"\u1086", RegexOptions.Compiled), "\u103F"),       // Zawgyi Great Sa -> Unicode Great Sa
        (new Regex(@"\u1087", RegexOptions.Compiled), "\u103C"),       // Zawgyi Ra-gyit -> Unicode Ra-gyit
        (new Regex(@"\u1088", RegexOptions.Compiled), "\u103C"),       // Zawgyi Ra-gyit -> Unicode Ra-gyit
        (new Regex(@"\u1089", RegexOptions.Compiled), "\u103C"),       // Zawgyi Ra-gyit -> Unicode Ra-gyit
        (new Regex(@"\u108A", RegexOptions.Compiled), "\u103C"),       // Zawgyi Ra-gyit -> Unicode Ra-gyit
        (new Regex(@"\u108B", RegexOptions.Compiled), "\u103D"),       // Zawgyi Wa-hswe -> Unicode Wa-hswe
        (new Regex(@"\u108C", RegexOptions.Compiled), "\u103E"),       // Zawgyi Ha-guth -> Unicode Ha-guth
        (new Regex(@"\u108D", RegexOptions.Compiled), "\u103D\u103E"), // Zawgyi Wa-hswe + Ha-guth -> Unicode ွှ
        (new Regex(@"\u108E", RegexOptions.Compiled), "\u103B\u103E"), // Zawgyi Ya-yit + Ha-guth -> Unicode ျှ
        (new Regex(@"\u108F", RegexOptions.Compiled), "\u1014"),       // Zawgyi Na -> Unicode Na
        (new Regex(@"\u1090", RegexOptions.Compiled), "\u1017"),       // Zawgyi Ba -> Unicode Ba
        (new Regex(@"\u1091", RegexOptions.Compiled), "\u1010"),       // Zawgyi Ta -> Unicode Ta
        (new Regex(@"\u1092", RegexOptions.Compiled), "\u1010"),       // Zawgyi Ta -> Unicode Ta
        (new Regex(@"\u1097", RegexOptions.Compiled), "\u1037"),       // Zawgyi Dot below -> Unicode Dot below
    };

    // Step 2: Zawgyi subjoined consonant mapping
    private static readonly (Regex Pattern, string Replacement)[] SubjoinedRules = new[]
    {
        (new Regex(@"\u1004\u1062", RegexOptions.Compiled), "\u1004\u1039\u1002"),   // Nga + Zawgyi subjoined Ga -> Unicode Nga + subjoined Ga
        (new Regex(@"\u1062", RegexOptions.Compiled), "\u1004\u103A\u1039\u1002"), // Standalone Zawgyi Kinzi/subjoined Ga -> Unicode Kinzi Ga (င်္ဂ)
        (new Regex(@"\u1064([\u1000-\u1021])", RegexOptions.Compiled), "\u1004\u103A\u1039$1"), // Zawgyi Kinzi + Consonant -> Unicode Kinzi + Consonant
        (new Regex(@"\u1064", RegexOptions.Compiled), "\u1004\u103A\u1039\u1002"), // Zawgyi Kinzi -> Unicode Kinzi Ga (င်္ဂ)
        (new Regex(@"\u1065", RegexOptions.Compiled), "\u1039\u1005"), // Subjoined Ca
        (new Regex(@"\u1066", RegexOptions.Compiled), "\u1039\u1006"), // Subjoined Cha
        (new Regex(@"\u1067", RegexOptions.Compiled), "\u1039\u1007"), // Subjoined Ja
        (new Regex(@"\u1068", RegexOptions.Compiled), "\u1039\u1008"), // Subjoined Jha
        (new Regex(@"\u1069", RegexOptions.Compiled), "\u1039\u1009"), // Subjoined Nya
        (new Regex(@"\u106C", RegexOptions.Compiled), "\u1039\u100B"), // Subjoined Tta
        (new Regex(@"\u106D", RegexOptions.Compiled), "\u1039\u100C"), // Subjoined Ttha
        (new Regex(@"\u106E", RegexOptions.Compiled), "\u1039\u100D"), // Subjoined Dda
        (new Regex(@"\u106F", RegexOptions.Compiled), "\u1039\u100E"), // Subjoined Ddha
        (new Regex(@"\u1070", RegexOptions.Compiled), "\u1039\u100F"), // Subjoined Nna
        (new Regex(@"\u1071", RegexOptions.Compiled), "\u1039\u1010"), // Subjoined Ta
        (new Regex(@"\u1072", RegexOptions.Compiled), "\u1039\u1011"), // Subjoined Tha
        (new Regex(@"\u1073", RegexOptions.Compiled), "\u1039\u1012"), // Subjoined Da
        (new Regex(@"\u1074", RegexOptions.Compiled), "\u1039\u1013"), // Subjoined Dha
        (new Regex(@"\u1075", RegexOptions.Compiled), "\u1014\u103A"), // Zawgyi final/subjoined Na -> Na + Asat
        (new Regex(@"\u1076", RegexOptions.Compiled), "\u1039\u1015"), // Subjoined Pa
        (new Regex(@"\u1077", RegexOptions.Compiled), "\u1039\u1016"), // Subjoined Pha
        (new Regex(@"\u1078", RegexOptions.Compiled), "\u1039\u1017"), // Subjoined Ba
        (new Regex(@"\u1079", RegexOptions.Compiled), "\u1039\u1018"), // Subjoined Bha
        (new Regex(@"\u107A", RegexOptions.Compiled), "\u1039\u1019"), // Subjoined Ma
        (new Regex(@"\u107B", RegexOptions.Compiled), "\u1039\u101C"), // Subjoined La
        (new Regex(@"\u107C", RegexOptions.Compiled), "\u1039\u101D"), // Subjoined Wa
        (new Regex(@"\u107D", RegexOptions.Compiled), "\u103B"),       // Zawgyi Ya-yit -> Unicode Ya-yit
        (new Regex(@"\u107E", RegexOptions.Compiled), "\u1039\u101E"), // Subjoined Sa
        (new Regex(@"\u107F", RegexOptions.Compiled), "\u1039\u1005"), // Subjoined Ca
        (new Regex(@"\u1080", RegexOptions.Compiled), "\u1039\u1006"), // Subjoined Cha
        (new Regex(@"\u1081", RegexOptions.Compiled), "\u1039\u1010"), // Subjoined Ta
        (new Regex(@"\u1082", RegexOptions.Compiled), "\u1039\u1011"), // Subjoined Tha
        (new Regex(@"\u1083", RegexOptions.Compiled), "\u1039\u1015"), // Subjoined Pa
        (new Regex(@"\u1084", RegexOptions.Compiled), "\u1039\u1019"), // Subjoined Ma
    };

    // Step 3: Visual E-vowel & Medial Reordering
    private static readonly (Regex Pattern, string Replacement)[] ReorderRules = new[]
    {
        // Reorder visual E-Vowel (U+1031) preceding consonant + subjoined + medials to follow them
        (new Regex(@"\u1031([\u1000-\u1021])(\u1039[\u1000-\u1021])?([\u103B-\u103E]*)", RegexOptions.Compiled), "$1$2$3\u1031"),
        // Reorder Ra-gyit (U+103C) preceding consonant to follow consonant
        (new Regex(@"\u103C([\u1000-\u1021])", RegexOptions.Compiled), "$1\u103C"),
    };

    // Step 4: Post-fixes (diacritics, tall vs curvature A-vowels, virama vs asat)
    private static readonly (Regex Pattern, string Replacement)[] PostFixRules = new[]
    {
        // Replace tall A-vowel U+102B with curvature A-vowel U+102C for non-tall consonants
        (new Regex(@"([^\u1001\u1002\u1004\u1012\u1015\u101D])\u1031\u102B", RegexOptions.Compiled), "$1\u1031\u102C"),
        (new Regex(@"([^\u1001\u1002\u1004\u1012\u1015\u101D])\u102B", RegexOptions.Compiled), "$1\u102C"),

        // Fix standalone Virama not preceding consonant to Asat
        (new Regex(@"\u1039(?![\u1000-\u1021])", RegexOptions.Compiled), "\u103A"),

        // Diacritic fixes & combinations
        (new Regex(@"\u102D\u1032", RegexOptions.Compiled), "\u102D\u102F"), // Zawgyi ို -> Unicode ို (U+102D U+102F)
        (new Regex(@"\u103A\u1037", RegexOptions.Compiled), "\u1037\u103A"), // Ordering of Asat & Dot below
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

        int myanmarCharCount = MyanmarRangeRegex.Matches(input).Count;
        if (myanmarCharCount == 0)
        {
            return new MyanmarScriptNormalizationResult(
                NormalizedText: input,
                OriginalText: input,
                IsZawgyiDetected: false,
                ConfidenceScore: 0.0,
                DetectedEncoding: MyanmarEncoding.NonMyanmar
            );
        }

        bool isZawgyi = DetectZawgyi(input, myanmarCharCount, out double confidenceScore);
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
        int myanmarCharCount = MyanmarRangeRegex.Matches(input).Count;
        if (myanmarCharCount == 0) return false;
        return DetectZawgyi(input, myanmarCharCount, out _);
    }

    private static bool DetectZawgyi(string input, int myanmarCharCount, out double confidenceScore)
    {
        int zawgyiFeatureMatches = ZawgyiExclusiveRegex.Matches(input).Count;
        if (zawgyiFeatureMatches > 0)
        {
            confidenceScore = Math.Min(1.0, (double)zawgyiFeatureMatches / Math.Max(1, myanmarCharCount / 4.0));
            return true;
        }

        confidenceScore = 0.0;
        return false;
    }

    private static string ConvertZawgyiToUnicode(string zawgyiText)
    {
        string result = zawgyiText;

        // Step 1: Glyph Pre-Substitutions
        foreach (var (pattern, replacement) in GlyphPreSubstitutions)
        {
            result = pattern.Replace(result, replacement);
        }

        // Step 2: Subjoined Consonants
        foreach (var (pattern, replacement) in SubjoinedRules)
        {
            result = pattern.Replace(result, replacement);
        }

        // Step 3: Reordering
        foreach (var (pattern, replacement) in ReorderRules)
        {
            result = pattern.Replace(result, replacement);
        }

        // Step 4: Post-Fixes
        foreach (var (pattern, replacement) in PostFixRules)
        {
            result = pattern.Replace(result, replacement);
        }

        return result;
    }
}
