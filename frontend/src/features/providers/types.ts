import { ProviderDiscipline } from "../schedule/types";

export { ProviderDiscipline };

export const DisciplineLabels: Record<ProviderDiscipline, string> = {
  [ProviderDiscipline.Other]: "Other",
  [ProviderDiscipline.PT]: "PT",
  [ProviderDiscipline.PTA]: "PTA",
};

// Matches PhysioTrac.Application.Scheduling.ProviderDto.
export interface Provider {
  id: string;
  firstName: string;
  lastName: string;
  fullName: string;
  specialty: string | null;
  credentials: string | null;
  npiNumber: string | null;
  isActive: boolean;
  onlineBookingEnabled: boolean;
  locationIds: string[];
  discipline: ProviderDiscipline;
  hasLogin: boolean;
}
