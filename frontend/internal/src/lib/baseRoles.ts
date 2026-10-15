import type { BaseRole } from "@recruitops/types";

/** The base roles a custom role may be built on (ADR-0021 option A), with what each reaches.
 *  Mirrors RoleScope.AssignableBaseRoles; never Admin or SuperAdmin — admin access is given by
 *  assigning the Admin system role. */
export const BASE_ROLES: ReadonlyArray<{
  value: BaseRole;
  label: string;
  reach: string;
}> = [
  {
    value: "HrDirector",
    label: "HR Director",
    reach: "Every department and candidate; can edit anyone’s requisitions.",
  },
  {
    value: "Recruiter",
    label: "Recruiter",
    reach: "The whole pipeline, across every department.",
  },
  {
    value: "HiringManager",
    label: "Hiring Manager",
    reach: "Their own departments only, plus candidates on interview panels they sit on.",
  },
  {
    value: "Approver",
    label: "Approver",
    reach: "Requisitions across every department, to approve them; no candidate data.",
  },
  {
    value: "Interviewer",
    label: "Interviewer",
    reach: "The narrowest: no pipeline, candidate or interview screens — only what the permissions below allow, in their own departments.",
  },
];

export function baseRoleLabel(
  role: BaseRole | null | undefined,
): string | null {
  return BASE_ROLES.find((r) => r.value === role)?.label ?? null;
}
