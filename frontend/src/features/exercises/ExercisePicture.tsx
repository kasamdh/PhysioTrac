import { useState } from "react";
import { exerciseImageUrl } from "./api";
import type { ExerciseImage } from "./types";

/** An exercise image, or -- when there is none yet, or it can't be
 * loaded -- a clearly labelled placeholder. Never substitutes another
 * picture: an image is only ever the one approved for this exercise. */
export function ExercisePicture({
  exerciseId,
  image,
  thumbnail = false,
  decorative = false,
  className = "",
}: {
  exerciseId: string;
  image: ExerciseImage | null | undefined;
  thumbnail?: boolean;
  /** Inside a control that already names it (e.g. a "Show step 2" button). */
  decorative?: boolean;
  className?: string;
}) {
  const [failed, setFailed] = useState(false);
  if (!image || failed)
    return (
      <div
        role="img"
        aria-label={failed ? "Image unavailable" : "Image pending approval"}
        className={`flex aspect-[4/3] flex-col items-center justify-center gap-1 rounded border border-dashed border-border bg-surface-muted p-2 text-center text-text-muted ${className}`}
      >
        <svg
          aria-hidden="true"
          viewBox="0 0 48 48"
          className="h-10 w-10 opacity-60"
        >
          <circle
            cx="24"
            cy="10"
            r="5"
            fill="none"
            stroke="currentColor"
            strokeWidth="2.5"
          />
          <path
            d="M24 15v15M24 30l-7 12M24 30l7 12M14 21l10 3 10-3"
            fill="none"
            stroke="currentColor"
            strokeWidth="2.5"
            strokeLinecap="round"
          />
        </svg>
        <span className="text-sm font-bold">
          {failed ? "Image unavailable" : "Image pending approval"}
        </span>
      </div>
    );
  return (
    <img
      src={exerciseImageUrl(exerciseId, image.id, thumbnail)}
      alt={decorative ? "" : image.altText}
      loading="lazy"
      width={image.width}
      height={image.height}
      onError={() => setFailed(true)}
      className={`h-auto w-full rounded border border-border bg-white object-contain ${className}`}
    />
  );
}
