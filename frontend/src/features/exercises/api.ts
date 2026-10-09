import { apiRequest, apiUrl } from "../../lib/apiClient";
import type {
  Exercise,
  ExerciseFilters,
  ExerciseImage,
  ExerciseInput,
  ExerciseSummary,
} from "./types";

export const exercisesKey = ["exercises"] as const;

export function searchExercises(filters: ExerciseFilters) {
  const params = new URLSearchParams();
  if (filters.q?.trim()) params.set("q", filters.q.trim());
  if (filters.equipment?.trim())
    params.set("equipment", filters.equipment.trim());
  for (const key of [
    "bodyRegion",
    "category",
    "difficulty",
    "position",
  ] as const) {
    const value = filters[key];
    if (value != null) params.set(key, String(value));
  }
  if (filters.includeInactive) params.set("includeInactive", "true");
  const query = params.toString();
  return apiRequest<ExerciseSummary[]>(
    `/api/v1/exercises${query ? `?${query}` : ""}`,
  );
}

export const fetchExercise = (id: string) =>
  apiRequest<Exercise>(`/api/v1/exercises/${id}`);
export const createExercise = (body: ExerciseInput) =>
  apiRequest<Exercise>("/api/v1/exercises", { method: "POST", body });
export const updateExercise = (id: string, body: ExerciseInput) =>
  apiRequest<Exercise>(`/api/v1/exercises/${id}`, { method: "PUT", body });
export const setExerciseActive = (id: string, active: boolean) =>
  apiRequest<Exercise>(
    `/api/v1/exercises/${id}/${active ? "activate" : "deactivate"}`,
    { method: "POST" },
  );
export const markExerciseReviewed = (id: string) =>
  apiRequest<Exercise>(`/api/v1/exercises/${id}/review`, { method: "POST" });

export interface ImageUpload {
  file: File;
  altText: string;
  caption: string;
  sourceAttribution: string;
  sequence?: number;
}

const form = (u: ImageUpload) => {
  const data = new FormData();
  data.append("file", u.file);
  data.append("altText", u.altText);
  if (u.caption.trim()) data.append("caption", u.caption.trim());
  if (u.sourceAttribution.trim())
    data.append("sourceAttribution", u.sourceAttribution.trim());
  if (u.sequence) data.append("sequence", String(u.sequence));
  return data;
};

export const uploadExerciseImage = (exerciseId: string, upload: ImageUpload) =>
  apiRequest<ExerciseImage>(`/api/v1/exercises/${exerciseId}/media`, {
    method: "POST",
    body: form(upload),
  });
export const replaceExerciseImage = (
  exerciseId: string,
  mediaId: string,
  upload: ImageUpload,
) =>
  apiRequest<ExerciseImage>(
    `/api/v1/exercises/${exerciseId}/media/${mediaId}/replace`,
    { method: "POST", body: form(upload) },
  );
export const updateExerciseImage = (
  exerciseId: string,
  mediaId: string,
  body: {
    altText: string;
    caption: string | null;
    sourceAttribution: string | null;
  },
) =>
  apiRequest<ExerciseImage>(
    `/api/v1/exercises/${exerciseId}/media/${mediaId}`,
    { method: "PUT", body },
  );
export const removeExerciseImage = (exerciseId: string, mediaId: string) =>
  apiRequest<void>(`/api/v1/exercises/${exerciseId}/media/${mediaId}`, {
    method: "DELETE",
  });
export const reorderExerciseImages = (exerciseId: string, mediaIds: string[]) =>
  apiRequest<ExerciseImage[]>(`/api/v1/exercises/${exerciseId}/media/order`, {
    method: "PUT",
    body: { mediaIds },
  });

/** The image address; access is checked by the API with the session cookie. */
export const exerciseImageUrl = (
  exerciseId: string,
  mediaId: string,
  thumbnail = false,
) =>
  apiUrl(
    `/api/v1/exercises/${exerciseId}/media/${mediaId}${thumbnail ? "?size=thumb" : ""}`,
  );
