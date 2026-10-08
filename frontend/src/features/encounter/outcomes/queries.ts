import { useQuery } from "@tanstack/react-query";
import { fetchOutcomeDefinitions } from "./api";

export const outcomesKey = (patientId: string) => [
  "chart",
  "outcomes",
  patientId,
];

/** The measure catalog (it only changes with a release). */
export function useOutcomeDefinitions() {
  return useQuery({
    queryKey: ["outcomes", "definitions"],
    queryFn: fetchOutcomeDefinitions,
    staleTime: Infinity,
  });
}
