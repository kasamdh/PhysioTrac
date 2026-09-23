import { useQuery } from "@tanstack/react-query";
import { useAuth } from "../features/auth/AuthProvider";
import { UserRoleLabels } from "../features/auth/types";
import { fetchPatients } from "../features/patients/api";

export function DashboardPage() {
  const { user } = useAuth();

  // Demonstrates real API integration end-to-end: cookie session -> Api ->
  // ITenantAccessService.PatientsFor (role/caseload-scoped) -> this list.
  const patientsQuery = useQuery({
    queryKey: ["patients"],
    queryFn: fetchPatients,
  });

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-xl font-semibold text-text">
          Welcome back{user ? `, ${user.username}` : ""}
        </h1>
        <p className="text-sm text-text-muted">{user ? UserRoleLabels[user.role] : ""}</p>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <div className="card">
          <p className="text-xs uppercase tracking-wide text-text-subtle">Patients in your caseload</p>
          <p className="mt-2 text-3xl font-semibold text-text">
            {patientsQuery.isLoading ? "…" : (patientsQuery.data?.length ?? "—")}
          </p>
        </div>
      </div>

      <div className="card">
        <h2 className="mb-3 text-base font-semibold text-text">Patients</h2>
        {patientsQuery.isLoading && <p className="text-text-muted">Loading…</p>}
        {patientsQuery.isError && (
          <p className="alert-error">Could not load patients: {patientsQuery.error.message}</p>
        )}
        {patientsQuery.data && patientsQuery.data.length === 0 && (
          <p className="text-text-muted">No patients found for your caseload.</p>
        )}
        {patientsQuery.data && patientsQuery.data.length > 0 && (
          <div className="overflow-hidden rounded-lg border border-border">
            <table className="data-table">
              <thead>
                <tr>
                  <th>MRN</th>
                  <th>Name</th>
                  <th>Age</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {patientsQuery.data.map((patient) => (
                  <tr key={patient.id}>
                    <td className="text-text-muted">{patient.medicalRecordNumber}</td>
                    <td className="font-medium text-text">{patient.fullName}</td>
                    <td>{patient.age}</td>
                    <td>{patient.status}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
