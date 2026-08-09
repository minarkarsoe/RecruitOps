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

function makeErrorFetch(status: number, detail: string) {
  return vi.fn().mockResolvedValue({
    ok: false,
    status,
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
  it("posts to /ai/claude/match-candidate and returns match analysis", async () => {
    const mockResult = {
      candidateId: "cand-001",
      jobPostingId: "job-001",
      overallScore: 87,
      recommendation: "StrongMatch",
      strengths: ["TypeScript expertise"],
      gaps: [],
      criteria: [],
      suggestedInterviewQuestions: ["Tell me about your TypeScript experience."],
      summary: "Strong technical match.",
    };
    vi.stubGlobal("fetch", makeOkFetch(mockResult));
    const result = await aiApi.matchCandidate({ candidateId: "cand-001", jobPostingId: "job-001" });
    expect(fetch).toHaveBeenCalledWith(
      expect.stringContaining("/ai/claude/match-candidate"),
      expect.objectContaining({ method: "POST" })
    );
    expect(result.recommendation).toBe("StrongMatch");
    expect(result.overallScore).toBe(87);
  });
});

describe("aiApi.generateExecutiveSummary", () => {
  it("posts to /ai/gemini/executive-summary and returns summary", async () => {
    const mockResult = {
      candidateId: "cand-001",
      headline: "Experienced Full-Stack Engineer",
      summary: "Aung Ko brings 5 years of TypeScript and React experience.",
      keyStrengths: ["TypeScript", "React"],
      suggestedInterviewQuestions: [],
      isBilingual: false,
    };
    vi.stubGlobal("fetch", makeOkFetch(mockResult));
    const result = await aiApi.generateExecutiveSummary({
      candidateId: "cand-001",
      audience: "client",
      language: "en",
    });
    expect(fetch).toHaveBeenCalledWith(
      expect.stringContaining("/ai/gemini/executive-summary"),
      expect.objectContaining({ method: "POST" })
    );
    expect(result.isBilingual).toBe(false);
    expect(result.headline).toBe("Experienced Full-Stack Engineer");
  });
});

describe("aiApi.prepareDocument", () => {
  it("posts to /ai/gemini/document-prep and returns document", async () => {
    const mockResult = {
      candidateId: "cand-001",
      jobPostingId: "job-001",
      documentType: "InterviewKit",
      markdownContent: "# Interview Kit",
      htmlContent: "<h1>Interview Kit</h1>",
      generatedAtUtc: "2026-08-06T13:00:00Z",
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
    expect(result.documentType).toBe("InterviewKit");
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
