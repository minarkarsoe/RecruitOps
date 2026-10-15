/**
 * AI API client integration tests (Milestone 3).
 *
 * These tests verify that:
 *  1. `aiApi` exposes all 5 required methods with the correct types.
 *  2. Each method constructs the correct HTTP method, path, and JSON body.
 *  3. Errors are surfaced as ApiError with the correct status code.
 */
import { describe, it, expect, vi, beforeEach } from "vitest";
import { aiApi, ApiError } from "./api";

function makeOkFetch(body: unknown) {
  return vi.fn().mockResolvedValue({
    ok: true,
    status: 200,
    text: async () => JSON.stringify(body),
    json: async () => body,
  });
}

// A real Response always has `headers`, so the double must too — `apiFetch` reads
// `Retry-After` off every failure to carry a lockout countdown (ADR-0016). A double that
// omits it made this test fail with a TypeError instead of the ApiError it asserts, which
// is the double being wrong rather than the code.
function makeErrorFetch(status: number, detail: string) {
  return vi.fn().mockResolvedValue({
    ok: false,
    status,
    headers: new Headers(),
    text: async () => JSON.stringify({ detail }),
    json: async () => ({ detail }),
  });
}

beforeEach(() => {
  localStorage.clear();
});

describe("aiApi.parseResume", () => {
  it("posts to /ai/claude/parse-resume and returns parsed result", async () => {
    const mockResult = {
      fullName: "Aung Ko",
      email: "aungko@example.com",
      skills: [{ name: "TypeScript" }],
      experience: [],
      education: [],
      confidenceScore: 0.92,
    };
    vi.stubGlobal("fetch", makeOkFetch(mockResult));
    const result = await aiApi.parseResume({ resumeText: "Aung Ko - Software Engineer" });
    expect(fetch).toHaveBeenCalledWith(
      expect.stringContaining("/ai/claude/parse-resume"),
      expect.objectContaining({ method: "POST" })
    );
    expect(result.fullName).toBe("Aung Ko");
    expect(result.confidenceScore).toBe(0.92);
  });

  it("throws ApiError on 400", async () => {
    vi.stubGlobal("fetch", makeErrorFetch(400, "ResumeText cannot be empty."));
    await expect(aiApi.parseResume({ resumeText: "" })).rejects.toBeInstanceOf(ApiError);
  });
});

describe("aiApi.matchCandidate", () => {
  // ⚠️ REWRITTEN 2026-08-28 — the third contract in this file that described an API nobody had
  // called. This mocked `candidateId`, `jobPostingId`, `overallScore`, `gaps`, `criteria`,
  // `suggestedInterviewQuestions` and `summary`, and asserted `recommendation === "StrongMatch"`.
  // The service returns `matchScore`, `overallVerdict`, `matchedSkills`, `missingSkills`,
  // `strengths`, `concerns`, `recommendation` — and `recommendation` is a sentence, not a grade.
  // Verified against the running service's OpenAPI *and* a live 200 before correcting.
  it("posts to /ai/claude/match-candidate and returns the API's shape", async () => {
    const mockResult = {
      matchScore: 87,
      overallVerdict: "Strong Fit",
      matchedSkills: ["TypeScript"],
      missingSkills: ["Kubernetes"],
      strengths: ["TypeScript expertise"],
      concerns: [],
      recommendation: "Proceed to Technical Deep Dive Interview.",
    };
    vi.stubGlobal("fetch", makeOkFetch(mockResult));
    const result = await aiApi.matchCandidate({ candidateId: "cand-001", jobPostingId: "job-001" });
    expect(fetch).toHaveBeenCalledWith(
      expect.stringContaining("/ai/claude/match-candidate"),
      expect.objectContaining({ method: "POST" })
    );
    expect(result.matchScore).toBe(87);
    expect(result.overallVerdict).toBe("Strong Fit");
    expect(result.missingSkills).toEqual(["Kubernetes"]);
  });

  it("sends only the two fields the API binds", async () => {
    vi.stubGlobal("fetch", makeOkFetch({
      matchScore: 0, overallVerdict: "", matchedSkills: [], missingSkills: [],
      strengths: [], concerns: [], recommendation: "",
    }));
    await aiApi.matchCandidate({ candidateId: "cand-001", jobPostingId: "job-001" });

    const body = JSON.parse(String((vi.mocked(fetch).mock.calls[0][1] as RequestInit).body));
    expect(Object.keys(body).sort()).toEqual(["candidateId", "jobPostingId"]);
  });
});

describe("aiApi.generateExecutiveSummary", () => {
  // ⚠️ THIS TEST IS WHY THE CONTRACT DRIFTED UNNOTICED. Until 2026-08-28 the mock below
  // returned `{ candidateId, summary, keyStrengths, suggestedInterviewQuestions, isBilingual }`
  // — the shape the FRONTEND wanted. The API returns
  // `{ headline, executiveSummary, keyHighlights, recommendedInterviewQuestions }`, and only
  // `headline` was ever common to both. The test passed and proved nothing, while the panel
  // rendered a headline over three blanks.
  //
  // The mock is now the API's real shape, taken from the running service's own OpenAPI
  // document. A mock tuned to the caller's wishes is not a test of a contract.
  it("posts to /ai/gemini/executive-summary and returns the API's shape", async () => {
    const mockResult = {
      headline: "Experienced Full-Stack Engineer",
      executiveSummary: "Aung Ko brings 5 years of TypeScript and React experience.",
      keyHighlights: ["TypeScript", "React"],
      recommendedInterviewQuestions: [],
    };
    vi.stubGlobal("fetch", makeOkFetch(mockResult));
    const result = await aiApi.generateExecutiveSummary({ candidateId: "cand-001" });
    expect(fetch).toHaveBeenCalledWith(
      expect.stringContaining("/ai/gemini/executive-summary"),
      expect.objectContaining({ method: "POST" })
    );
    expect(result.headline).toBe("Experienced Full-Stack Engineer");
    expect(result.executiveSummary).toBe("Aung Ko brings 5 years of TypeScript and React experience.");
    expect(result.keyHighlights).toEqual(["TypeScript", "React"]);
  });

  it("sends only the fields the API binds — no audience, no language", async () => {
    vi.stubGlobal("fetch", makeOkFetch({
      headline: "h", executiveSummary: "s", keyHighlights: [], recommendedInterviewQuestions: [],
    }));

    await aiApi.generateExecutiveSummary({ candidateId: "cand-001", jobPostingId: "job-001" });

    const body = JSON.parse(String((vi.mocked(fetch).mock.calls[0][1] as RequestInit).body));
    // `audience` was agency-era (ADR-0001 deleted clients) and `language` has never existed on
    // the request record. Both used to be sent and silently discarded by model binding.
    expect(body).toEqual({ candidateId: "cand-001", jobPostingId: "job-001" });
    expect(body).not.toHaveProperty("audience");
    expect(body).not.toHaveProperty("language");
  });
});

describe("aiApi.prepareDocument", () => {
  // ⚠️ REWRITTEN 2026-08-28. This mocked `candidateId`, `jobPostingId`, `documentType`,
  // `markdownContent`, `htmlContent` and `generatedAtUtc` — SIX fields, none of which the API
  // returns — and then asserted on `result.documentType`, a field that has never existed. The
  // test passed because the mock and the interface agreed with each other and neither had been
  // compared to the service. Verified against the running OpenAPI document: the response is
  // `documentTitle`, `contentMarkdown`, `contentHtml`.
  it("posts to /ai/gemini/document-prep and returns the document the API actually sends", async () => {
    const mockResult = {
      documentTitle: "Candidate Interview Kit & Assessment Guide",
      contentMarkdown: "# Interview Kit",
      contentHtml: "<h1>Interview Kit</h1>",
    };
    vi.stubGlobal("fetch", makeOkFetch(mockResult));
    const result = await aiApi.prepareDocument({
      candidateId: "cand-001",
      jobPostingId: "job-001",
      documentType: "InterviewKit",
    });
    expect(fetch).toHaveBeenCalledWith(
      expect.stringContaining("/ai/gemini/document-prep"),
      expect.objectContaining({ method: "POST" })
    );
    expect(result.documentTitle).toBe("Candidate Interview Kit & Assessment Guide");
    expect(result.contentMarkdown).toContain("Interview Kit");
    expect(result.contentHtml).toContain("<h1>");
  });

  it("sends only the three fields the API binds", async () => {
    // `language` used to be on the request interface and the API record never had one, so it was
    // discarded — the same bug as the Executive Summary's. Nothing should reintroduce it here
    // without the server side landing in the same change.
    vi.stubGlobal("fetch", makeOkFetch({ documentTitle: "t", contentMarkdown: "#", contentHtml: "<p/>" }));
    await aiApi.prepareDocument({
      candidateId: "cand-001",
      jobPostingId: "job-001",
      documentType: "JdDraft",
    });

    const body = JSON.parse((fetch as unknown as ReturnType<typeof vi.fn>).mock.calls[0][1].body);
    expect(Object.keys(body).sort()).toEqual(["candidateId", "documentType", "jobPostingId"]);
  });
});

describe("aiApi.translateBurmese", () => {
  it("posts to /ai/gemini/burmese-localization and returns translation", async () => {
    const mockResult = {
      originalText: "Hello, welcome to RecruitOps.",
      translatedText: "\u1019\u1004\u103a\u1039\u1002\u101c\u102c\u1015\u102b\u1010\u102c RecruitOps \u101e\u102d\u1037 \u1000\u103c\u102d\u102f\u1006\u102d\u102f\u1015\u102b\u101e\u100a\u1037\u104b",
      targetLanguage: "my",
      confidenceScore: 0.96,
    };
    vi.stubGlobal("fetch", makeOkFetch(mockResult));
    const result = await aiApi.translateBurmese({
      sourceText: "Hello, welcome to RecruitOps.",
      targetLanguage: "my",
    });
    expect(fetch).toHaveBeenCalledWith(
      expect.stringContaining("/ai/gemini/burmese-localization"),
      expect.objectContaining({ method: "POST" })
    );
    expect(result.targetLanguage).toBe("my");
    expect(result.confidenceScore).toBeGreaterThan(0.9);
  });

  it("throws ApiError on 422", async () => {
    vi.stubGlobal("fetch", makeErrorFetch(422, "SourceText and TargetLanguage are required."));
    await expect(
      aiApi.translateBurmese({ sourceText: "", targetLanguage: "my" })
    ).rejects.toBeInstanceOf(ApiError);
  });
});
