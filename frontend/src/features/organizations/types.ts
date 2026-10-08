// Matches PhysioTrac.Api.Controllers.CurrentOrganizationDto / LocationSummaryDto.
export interface LocationSummary {
  id: string;
  name: string;
}

export interface CurrentOrganization {
  id: string;
  name: string;
  locations: LocationSummary[];
  /** IANA zone for displaying times (printouts). */
  timezone: string;
}
