// Mirrors PhysioTrac.Domain.Enums.Exercise* (values are stored; append only).
export const BodyRegionLabels: Record<number, string> = {
  0: "Neck",
  1: "Shoulder",
  2: "Elbow, wrist & hand",
  3: "Upper back (thoracic)",
  4: "Low back",
  5: "Hip",
  6: "Knee",
  7: "Ankle & foot",
  8: "Core",
  9: "Pelvis",
  10: "Whole body",
};

export const CategoryLabels: Record<number, string> = {
  0: "Neck and cervical spine",
  1: "Shoulder",
  2: "Elbow, wrist and hand",
  3: "Thoracic spine",
  4: "Low back",
  5: "Hip",
  6: "Knee",
  7: "Ankle and foot",
  8: "Core strengthening",
  9: "Balance and coordination",
  10: "Gait training",
  11: "Posture and ergonomics",
  12: "Stretching and flexibility",
  13: "Strengthening",
  14: "Range of motion",
  15: "Neuromuscular re-education",
  16: "Post-operative rehabilitation",
  17: "Pelvic health",
  18: "Sports rehabilitation",
  19: "Fall prevention",
  20: "Home safety",
};

export const DifficultyLabels: Record<number, string> = {
  0: "Beginner",
  1: "Intermediate",
  2: "Advanced",
};

export const PositionLabels: Record<number, string> = {
  0: "Standing",
  1: "Sitting",
  2: "Lying on back",
  3: "Lying face down",
  4: "Side-lying",
  5: "Kneeling",
  6: "Other",
};

export const LateralityLabels: Record<number, string> = {
  0: "Not applicable",
  1: "Left",
  2: "Right",
  3: "Both sides",
  4: "Either side",
};

// Matches ExerciseMediaDto.
export interface ExerciseImage {
  id: string;
  sequence: number;
  altText: string;
  caption: string | null;
  sourceAttribution: string | null;
  width: number;
  height: number;
  contentType: string;
  isCurrent: boolean;
  createdAt: string;
}

// Matches ExerciseSummaryDto.
export interface ExerciseSummary {
  id: string;
  code: string;
  name: string;
  patientDescription: string | null;
  bodyRegion: number;
  category: number;
  equipment: string | null;
  difficulty: number;
  position: number;
  isActive: boolean;
  isPlatform: boolean;
  needsClinicalReview: boolean;
  coverImage: ExerciseImage | null;
  imageCount: number;
}

// Matches SaveExerciseRequest.
export interface ExerciseInput {
  name: string;
  patientDescription: string | null;
  clinicalPurpose: string | null;
  bodyRegion: number;
  targetMuscles: string | null;
  category: number;
  startingPosition: string | null;
  instructions: string | null;
  endingPosition: string | null;
  equipment: string | null;
  difficulty: number;
  position: number;
  laterality: number;
  breathingInstructions: string | null;
  commonMistakes: string | null;
  safetyPrecautions: string | null;
  contraindications: string | null;
  progressions: string | null;
  regressions: string | null;
  videoUrl: string | null;
}

// Matches ExerciseDto.
export interface Exercise extends ExerciseInput {
  id: string;
  code: string;
  isActive: boolean;
  isPlatform: boolean;
  needsClinicalReview: boolean;
  reviewedAt: string | null;
  reviewedByName: string | null;
  canEdit: boolean;
  images: ExerciseImage[];
  createdAt: string;
  createdByName: string | null;
  updatedAt: string;
  updatedByName: string | null;
}

export interface ExerciseFilters {
  q?: string;
  bodyRegion?: number | null;
  category?: number | null;
  equipment?: string;
  difficulty?: number | null;
  position?: number | null;
  includeInactive?: boolean;
}

/** "Step 1", or the image's own caption. */
export const stepLabel = (image: ExerciseImage, total: number) =>
  total > 1
    ? `Step ${image.sequence}${image.caption ? ` — ${image.caption}` : ""}`
    : (image.caption ?? "");
