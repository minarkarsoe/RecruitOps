# Progress Log

Last visited: 2026-08-07T21:50:00Z

- [x] Create `InMemoryFileStorage.cs` in `RecruitOps.Api.Tests`
- [x] Configure `CustomWebAppFactory.cs` to inject `InMemoryFileStorage` singleton for offline test isolation
- [x] Fix image metadata extraction in `DocumentTextExtractor.cs` (no hardcoded string placeholders)
- [x] Update `PhoneRegex` in `DocumentTextExtractor.cs` to support spaced/hyphenated formats like `+95 9 1234 5678`
- [x] Update `SkillKeywords` regex boundary matching for non-word boundary skills like `C#` and `.NET`
- [x] Add header filter list in `DocumentTextExtractor.cs` for candidate name extraction
- [x] Optimize memory stream preallocation in `ResumeService.cs`
- [x] Fix Zawgyi-to-Unicode Kinzi normalization rules in `MyanmarScriptNormalizer.cs`
- [x] Run full solution test suite `dotnet test backend/RecruitOps.sln` — 349/349 tests pass (100% pass rate)
