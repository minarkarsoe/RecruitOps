import { describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { AuditLogEntry, PagedResult } from "@recruitops/types";
import { AuditLogPage } from "./AuditLogPage";
import { api } from "../lib/api";

vi.mock("../lib/api", () => ({ api: vi.fn() }));

const apiMock = api as unknown as ReturnType<typeof vi.fn>;

function entry(over: Partial<AuditLogEntry> = {}): AuditLogEntry {
  return {
    id: over.id ?? "a-1",
    occurredAt: "2026-10-15T07:28:00Z",
    action: "RoleUpdated",
    actionLabel: "Role changed",
    isRefusal: false,
    actorUserId: "usr-1",
    actorName: "Aung Ko Ko",
    targetType: "Role",
    targetId: "role-1",
    targetName: "Finance Approver",
    summary:
      "Changed role “Finance Approver”: base Approver → Recruiter; 3 people’s access moved.",
    details: {
      baseRole: { from: "Approver", to: "Recruiter" },
      permissionsAdded: ["permission:postings:postings:read"],
    },
    ipAddress: "10.0.4.9",
    traceId: "00-abc-def-01",
    ...over,
  };
}

function page(
  items: AuditLogEntry[],
  over: Partial<PagedResult<AuditLogEntry>> = {},
): PagedResult<AuditLogEntry> {
  return {
    items,
    page: 1,
    pageSize: 25,
    totalCount: items.length,
    totalPages: 1,
    ...over,
  };
}

function inTable() {
  return within(screen.getByRole("table"));
}

describe("AuditLogPage", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("shows who did what, when, and from where", async () => {
    apiMock.mockResolvedValue(page([entry()]));
    render(<AuditLogPage />);

    expect(await screen.findByText("Aung Ko Ko")).toBeInTheDocument();
    expect(inTable().getByText("Role changed")).toBeInTheDocument();
    expect(
      inTable().getByText(/base Approver → Recruiter/),
    ).toBeInTheDocument();
    expect(inTable().getByText("10.0.4.9")).toBeInTheDocument();
    expect(apiMock).toHaveBeenCalledWith("/audit-log?page=1&pageSize=25");
  });

  it("colours a refusal red, and nothing else", async () => {
    apiMock.mockResolvedValue(
      page([
        entry({
          id: "r",
          action: "AccessChangeRefused",
          actionLabel: "Access change refused",
          isRefusal: true,
        }),
        entry({ id: "ok", actionLabel: "Role created", action: "RoleCreated" }),
      ]),
    );
    render(<AuditLogPage />);

    const refused = await screen.findByText("Access change refused");
    expect(refused.className).toContain("critical");
    expect(inTable().getByText("Role created").className).not.toContain(
      "critical",
    );
  });

  it("labels a failed sign-in by the email tried, as not an account holder", async () => {
    apiMock.mockResolvedValue(
      page([
        entry({
          action: "SignInFailed",
          actionLabel: "Sign-in failed",
          isRefusal: true,
          actorUserId: null,
          actorName: "ghost@company.test",
          targetType: null,
          targetId: null,
          targetName: null,
          summary:
            "Sign-in failed for ghost@company.test: no account with this email.",
        }),
      ]),
    );
    render(<AuditLogPage />);

    expect(await screen.findByText("ghost@company.test")).toBeInTheDocument();
    expect(inTable().getByText("not signed in")).toBeInTheDocument();
  });

  it("names nobody for a stolen session token, and says why", async () => {
    apiMock.mockResolvedValue(
      page([
        entry({
          action: "RefreshTokenReused",
          actionLabel: "Stolen session token used",
          isRefusal: true,
          actorUserId: null,
          actorName: null,
          targetType: "User",
          targetName: "Tester",
          summary: "A revoked session token for Tester was used again — 1 active session was ended.",
        }),
      ]),
    );
    render(<AuditLogPage />);

    expect(await screen.findByText("Unknown")).toBeInTheDocument();
    expect(inTable().getByText("held a stolen session token")).toBeInTheDocument();
    expect(inTable().getByText("Stolen session token used").className).toContain("critical");
  });

  it("filters by kind through the server", async () => {
    apiMock.mockResolvedValue(page([entry()]));
    render(<AuditLogPage />);
    await screen.findByText("Aung Ko Ko");

    await userEvent.click(screen.getByRole("button", { name: "Refused" }));

    await waitFor(() =>
      expect(apiMock).toHaveBeenLastCalledWith(
        "/audit-log?page=1&pageSize=25&category=Refused",
      ),
    );
    expect(screen.getByRole("button", { name: "Refused" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
  });

  it("shows the before/after on request, not in the row", async () => {
    apiMock.mockResolvedValue(page([entry()]));
    render(<AuditLogPage />);
    await screen.findByText("Aung Ko Ko");

    const details = inTable().getByText("Details");
    expect(details.closest("details")).not.toHaveAttribute("open");
    await userEvent.click(details);
    expect(
      inTable().getByText(/permission:postings:postings:read/),
    ).toBeVisible();
  });

  it("pages back through older entries", async () => {
    apiMock.mockResolvedValue(
      page([entry()], { totalCount: 60, totalPages: 3 }),
    );
    render(<AuditLogPage />);
    await screen.findByText("Aung Ko Ko");

    expect(screen.getByRole("button", { name: "Newer" })).toBeDisabled();
    await userEvent.click(screen.getByRole("button", { name: "Older" }));
    await waitFor(() =>
      expect(apiMock).toHaveBeenLastCalledWith("/audit-log?page=2&pageSize=25"),
    );
  });

  it("says so when nothing has been recorded yet", async () => {
    apiMock.mockResolvedValue(page([]));
    render(<AuditLogPage />);
    expect(await screen.findByText("Nothing recorded yet")).toBeInTheDocument();
  });

  it("shows the error when the log cannot be read", async () => {
    apiMock.mockRejectedValue(
      new Error("You do not have permission to view this."),
    );
    render(<AuditLogPage />);
    expect(await screen.findByRole("alert")).toHaveTextContent(
      "You do not have permission to view this.",
    );
  });
});
