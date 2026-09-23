// Matches PhysioTrac.Api.Controllers.CurrentOrganizationDto / LocationSummaryDto.
export interface LocationSummary {
  id: string;
  name: string;
}

export interface CurrentOrganization {
  id: string;
  name: string;
  locations: LocationSummary[];
}
