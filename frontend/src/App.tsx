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
                  <Route element={<AppLayout />}>
                    <Route path="/" element={<DashboardPage />} />
                    <Route path="/patients" element={<PatientListPage title="Patients" back={null} allowAdd />} />
                    <Route path="/schedule" element={<SchedulePage />} />
                    <Route path="/schedule/hours" element={<ProviderHoursPage />} />
                    <Route path="/providers" element={<ProvidersPage />} />
                    <Route path="/billing" element={<PlaceholderPage title="Billing" />} />
                    <Route path="/admin" element={<AdminHomePage />} />
                    <Route path="/admin/change-password" element={<ChangePasswordPage />} />
                    <Route path="/admin/messages" element={<MessagesPage />} />
                    <Route path="/admin/users" element={<UsersAdminPage />} />
                    <Route path="/admin/locations" element={<LocationsAdminPage />} />
                    <Route path="/admin/patients" element={<PatientListPage />} />
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
