// Matches PhysioTrac.Application.Clinical.SpecialTestDefinitionDto.
export interface SpecialTestDefinition {
  id: string;
  code: string;
  name: string;
  specialty: number;
  bodyRegion: string | null;
  description: string | null;
  resultKind: number; // 0 positive/negative, 1 numeric, 2 both
  unit: string | null;
  interpretationGuide: string | null;
  contraindicationWarning: string | null;
  isActive: boolean;
  isSystem: boolean;
  isFavorite: boolean;
}

// Matches SpecialTestResultDto (+ the library definition while editing).
export interface SpecialTest {
  testName: string;
  outcome: number; // 0 not tested, 1 positive, 2 negative
  definitionId?: string | null;
  specialty: number;
  bodyRegion?: string | null;
  side?: number | null;
  numericValue?: number | null;
  unit?: string | null;
  interpretation?: string | null;
  comment?: string | null;
  id?: string | null;
  /** Client-only: the library entry (warning, guide, result kind). Not sent. */
  definition?: SpecialTestDefinition;
}

// Matches SpecialTestHistoryDto.
export interface SpecialTestHistory {
  testName: string;
  side: number | null;
  serviceDate: string;
  outcome: number;
  numericValue: number | null;
  unit: string | null;
}
