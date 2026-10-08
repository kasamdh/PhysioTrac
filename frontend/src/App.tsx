import { QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter, Route, Routes } from "react-router-dom";
import { queryClient } from "./lib/queryClient";
import { AuthProvider } from "./features/auth/AuthProvider";
import { ProtectedRoute } from "./components/ProtectedRoute";
import { ErrorBoundary } from "./components/ErrorBoundary";
import { ToastProvider } from "./components/Toast";
import { AppLayout } from "./components/layout/AppLayout";
import { LoginPage } from "./pages/LoginPage";
import { DashboardPage } from "./pages/DashboardPage";
import { PlaceholderPage } from "./pages/PlaceholderPage";
import { SchedulePage } from "./features/schedule/SchedulePage";
import { ProviderHoursPage } from "./features/schedule/ProviderHoursPage";
import { AdminHomePage } from "./features/admin/pages/AdminHomePage";
import { LocationsAdminPage } from "./features/admin/pages/LocationsAdminPage";
import { UsersAdminPage } from "./features/admin/pages/UsersAdminPage";
import { MessagesPage } from "./features/admin/pages/MessagesPage";
import { PatientListPage } from "./features/admin/pages/PatientListPage";
import { ChangePasswordPage } from "./features/admin/pages/ChangePasswordPage";
import { ActivateAccountPage } from "./features/admin/pages/ActivateAccountPage";
import { ProvidersPage } from "./features/providers/ProvidersPage";
import { WorkflowPage } from "./features/workflow/WorkflowPage";
import { LogsPage } from "./features/logs/LogsPage";
import { EncounterPage } from "./features/encounter/EncounterPage";
import { NotePrintPage } from "./features/charting/NotePrintPage";
import { TemplatesPage } from "./features/templates/pages/TemplatesPage";
import { TemplateEditorPage } from "./features/templates/pages/TemplateEditorPage";
import { SpecialTestsAdminPage } from "./features/encounter/measurements/SpecialTestsAdminPage";
import { PatientDocumentationPage } from "./features/documentation/PatientDocumentationPage";

export default function App() {
  return (
    <ErrorBoundary>
      <QueryClientProvider client={queryClient}>
        <ToastProvider>
          <BrowserRouter>
            <AuthProvider>
              <Routes>
                <Route path="/login" element={<LoginPage />} />
                {/* One-time link from Administration › Users (InviteAsync's activation URL). */}
                <Route path="/:orgSlug/activate" element={<ActivateAccountPage />} />
                <Route element={<ProtectedRoute />}>
                  {/* A printable document: no app header or navigation. */}
                  <Route path="/notes/:noteId/print" element={<NotePrintPage />} />
                  <Route element={<AppLayout />}>
                    <Route path="/" element={<DashboardPage />} />
                    <Route path="/patients" element={<PatientListPage title="Patients" back={null} />} />
                    <Route path="/schedule" element={<SchedulePage />} />
                    <Route path="/schedule/hours" element={<ProviderHoursPage />} />
                    <Route path="/providers" element={<ProvidersPage />} />
                    <Route path="/workflow" element={<WorkflowPage />} />
                    <Route path="/chart/:noteId" element={<EncounterPage />} />
                    <Route path="/patients/:patientId/documentation" element={<PatientDocumentationPage />} />
                    <Route path="/billing" element={<PlaceholderPage title="Billing" />} />
                    <Route path="/admin" element={<AdminHomePage />} />
                    <Route path="/admin/change-password" element={<ChangePasswordPage />} />
                    <Route path="/admin/messages" element={<MessagesPage />} />
                    <Route path="/admin/users" element={<UsersAdminPage />} />
                    <Route path="/admin/locations" element={<LocationsAdminPage />} />
                    <Route path="/admin/patients" element={<PatientListPage manage />} />
                    <Route path="/admin/logs" element={<LogsPage />} />
                    <Route path="/admin/templates" element={<TemplatesPage />} />
                    <Route path="/admin/templates/new" element={<TemplateEditorPage />} />
                    <Route path="/admin/templates/:templateId" element={<TemplateEditorPage />} />
                    <Route path="/admin/special-tests" element={<SpecialTestsAdminPage />} />
                  </Route>
                </Route>
              </Routes>
            </AuthProvider>
          </BrowserRouter>
        </ToastProvider>
      </QueryClientProvider>
    </ErrorBoundary>
  );
}
