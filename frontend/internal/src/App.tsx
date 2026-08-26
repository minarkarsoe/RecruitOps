import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AppLayout } from './components/AppLayout';
import { RequireAuth } from './components/RequireAuth';
import { RequirePermission } from './components/RequirePermission';
import { LoginPage } from './pages/LoginPage';
import { RequisitionsPage } from './pages/RequisitionsPage';
import { RequisitionDetailPage } from './pages/RequisitionDetailPage';
import { RequisitionFormPage } from './pages/RequisitionFormPage';
import { JobPostingsPage } from './pages/JobPostingsPage';
import { JobPostingDetailPage } from './pages/JobPostingDetailPage';
import { InterviewDetailPage } from './pages/InterviewDetailPage';
import { InboxPage } from './pages/InboxPage';
import { ApprovalChainsPage } from './pages/ApprovalChainsPage';
import { JdTemplatesPage } from './pages/JdTemplatesPage';
import { ScorecardTemplatesPage } from './pages/ScorecardTemplatesPage';
import { DepartmentsPage } from './pages/DepartmentsPage';
import { UsersPage } from './pages/UsersPage';
import { RolesPage } from './pages/RolesPage';
import { AnalyticsPage } from './pages/AnalyticsPage';
import { DeliveryLogPage } from './pages/DeliveryLogPage';

export function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<LoginPage />} />

        <Route element={<RequireAuth><AppLayout /></RequireAuth>}>
          {/* Requisitions (all roles) */}
          <Route path="/requisitions" element={<RequisitionsPage />} />
          {/* "new" is declared before ":id" so it isn't swallowed by the param route. */}
          <Route path="/requisitions/new" element={<RequisitionFormPage mode="create" />} />
          <Route path="/requisitions/:id" element={<RequisitionDetailPage />} />
          <Route path="/requisitions/:id/edit" element={<RequisitionFormPage mode="edit" />} />

          {/* Module 2 — postings and pipeline */}
          <Route path="/jobpostings" element={<JobPostingsPage />} />
          <Route path="/jobpostings/:id" element={<JobPostingDetailPage />} />

          {/* Module 3 — one interview round. Not nested under a posting: a panel member
              from another department reaches this round and nothing else around it
              (ADR-0017 §4), so the URL must not imply access to the posting. */}
          <Route path="/interviews/:id" element={<InterviewDetailPage />} />

          {/* Approver / Admin inbox */}
          <Route path="/inbox" element={<InboxPage />} />

          {/* Admin + Recruiter + HrDirector */}
          <Route path="/jdtemplates" element={<JdTemplatesPage />} />
          {/* Readable by any internal user — an interviewer should be able to see what they
              will be asked before the day; the page hides its editing affordances itself. */}
          <Route path="/scorecardtemplates" element={<ScorecardTemplatesPage />} />

          {/* Admin only */}
          <Route path="/approvalchains" element={<ApprovalChainsPage />} />
          <Route path="/departments" element={<DepartmentsPage />} />

          {/* Module 4 — User Directory & Role Builder */}
          <Route
            path="/users"
            element={
              <RequirePermission permission="permission:users:users:read">
                <UsersPage />
              </RequirePermission>
            }
          />
          <Route
            path="/roles"
            element={
              <RequirePermission permission="permission:roles:roles:read">
                <RolesPage />
              </RequirePermission>
            }
          />
          {/* Module 5 — Reporting & Analytics */}
          <Route path="/analytics" element={<AnalyticsPage />} />

          {/* ADR-0026 — the delivery log. No <RequirePermission> wrapper: reach here is not a
              permission but a role-and-department question, and it is answered server-side
              (ADR-0003 / ADR-0018). An Approver gets an empty log rather than a 403, which is
              correct — they are allowed to ask, and the answer is nothing. */}
          <Route path="/delivery" element={<DeliveryLogPage />} />
        </Route>

        <Route path="*" element={<Navigate to="/requisitions" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

