import { useRef, useState, type FormEvent } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useToast } from "../../components/Toast";
import {
  exercisesKey,
  removeExerciseImage,
  reorderExerciseImages,
  replaceExerciseImage,
  updateExerciseImage,
  uploadExerciseImage,
} from "./api";
import { ExercisePicture } from "./ExercisePicture";
import type { Exercise, ExerciseImage } from "./types";

const ACCEPT = "image/jpeg,image/png,image/webp";
const MAX_MB = 10;

/** The exercise's images in sequence order: upload, describe (alternative
 * text), reorder, replace and remove. Replacing or removing keeps the old
 * image for programs that already used it. */
export function ExerciseImageManager({ exercise }: { exercise: Exercise }) {
  const queryClient = useQueryClient();
  const { showToast } = useToast();
  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: exercisesKey });
  };
  const images = exercise.images;
  const canEdit = exercise.canEdit;

  const move = useMutation({
    mutationFn: (order: string[]) => reorderExerciseImages(exercise.id, order),
    onSuccess: refresh,
    onError: (e: Error) => showToast(e.message),
  });
  const remove = useMutation({
    mutationFn: (img: ExerciseImage) =>
      removeExerciseImage(exercise.id, img.id),
    onSuccess: () => {
      showToast("Image removed. Programs that used it still show it.");
      refresh();
    },
    onError: (e: Error) => showToast(e.message),
  });
  const swap = (i: number, j: number) => {
    const order = images.map((m) => m.id);
    [order[i], order[j]] = [order[j], order[i]];
    move.mutate(order);
  };

  return (
    <section aria-labelledby="ex-images" className="card space-y-4">
      <h2 id="ex-images" className="text-2xl font-bold text-[#1565b8]">
        Images
      </h2>
      <p className="text-text-muted">
        JPEG, PNG or WebP, up to {MAX_MB} MB. Use only images your clinic owns
        or is licensed to use, that show this exact exercise correctly. For a
        movement, upload the steps in order: starting position, movement, ending
        position.
      </p>
      {images.length === 0 && (
        <p className="text-text-muted">
          No images yet. Patients will see an “Image pending approval”
          placeholder.
        </p>
      )}
      <ol className="space-y-3">
        {images.map((img, i) => (
          <ImageRow
            key={img.id}
            exerciseId={exercise.id}
            image={img}
            total={images.length}
            canEdit={canEdit}
            busy={move.isPending || remove.isPending}
            onUp={i > 0 ? () => swap(i, i - 1) : undefined}
            onDown={i < images.length - 1 ? () => swap(i, i + 1) : undefined}
            onRemove={() => {
              if (
                window.confirm(
                  `Remove step ${img.sequence}? Programs that already used it keep showing it.`,
                )
              )
                remove.mutate(img);
            }}
            onChanged={refresh}
          />
        ))}
      </ol>
      {canEdit && images.length < 8 && (
        <UploadForm
          // A fresh form after each upload: the step number resets to "last".
          key={images.length}
          exerciseId={exercise.id}
          next={images.length + 1}
          onDone={refresh}
        />
      )}
    </section>
  );
}

function ImageRow({
  exerciseId,
  image,
  total,
  canEdit,
  busy,
  onUp,
  onDown,
  onRemove,
  onChanged,
}: {
  exerciseId: string;
  image: ExerciseImage;
  total: number;
  canEdit: boolean;
  busy: boolean;
  onUp?: () => void;
  onDown?: () => void;
  onRemove: () => void;
  onChanged: () => void;
}) {
  const { showToast } = useToast();
  const [editing, setEditing] = useState(false);
  const [alt, setAlt] = useState(image.altText);
  const [caption, setCaption] = useState(image.caption ?? "");
  const [source, setSource] = useState(image.sourceAttribution ?? "");
  const fileRef = useRef<HTMLInputElement>(null);
  const save = useMutation({
    mutationFn: () =>
      updateExerciseImage(exerciseId, image.id, {
        altText: alt,
        caption: caption || null,
        sourceAttribution: source || null,
      }),
    onSuccess: () => {
      setEditing(false);
      onChanged();
    },
  });
  const replace = useMutation({
    mutationFn: (file: File) =>
      replaceExerciseImage(exerciseId, image.id, {
        file,
        altText: alt,
        caption,
        sourceAttribution: source,
      }),
    onSuccess: () => {
      showToast(
        `Step ${image.sequence} replaced. Programs that used the old image keep it.`,
      );
      onChanged();
    },
    onError: (e: Error) => showToast(e.message),
  });

  return (
    <li className="grid gap-3 rounded-md border border-border p-3 sm:grid-cols-[10rem_1fr]">
      <div className="relative">
        {total > 1 && (
          <span className="absolute top-1 left-1 z-10 rounded-full bg-[#1565b8] px-2 text-sm font-bold text-white">
            {image.sequence}
          </span>
        )}
        <ExercisePicture exerciseId={exerciseId} image={image} thumbnail />
      </div>
      <div className="min-w-0 space-y-2">
        {!editing ? (
          <>
            <p className="font-bold text-[#333]">
              {total > 1 ? `Step ${image.sequence}` : "Image"}
              {image.caption ? ` — ${image.caption}` : ""}
            </p>
            <p className="text-text-muted">Description: {image.altText}</p>
            {image.sourceAttribution && (
              <p className="text-text-muted">
                Source: {image.sourceAttribution}
              </p>
            )}
          </>
        ) : (
          <div className="space-y-2">
            <TextField
              id={`alt-${image.id}`}
              label="Description for screen readers (required)"
              value={alt}
              onChange={setAlt}
            />
            <TextField
              id={`cap-${image.id}`}
              label="Step caption"
              value={caption}
              onChange={setCaption}
            />
            <TextField
              id={`src-${image.id}`}
              label="Source / licence"
              value={source}
              onChange={setSource}
            />
            {save.isError && (
              <p className="alert-error">{save.error.message}</p>
            )}
          </div>
        )}
        {canEdit && (
          <div className="flex flex-wrap gap-2">
            {editing ? (
              <>
                <button
                  type="button"
                  className="btn-primary"
                  disabled={!alt.trim() || save.isPending}
                  onClick={() => save.mutate()}
                >
                  Save
                </button>
                <button
                  type="button"
                  className="btn-refresh"
                  onClick={() => setEditing(false)}
                >
                  Cancel
                </button>
              </>
            ) : (
              <>
                <button
                  type="button"
                  className="btn-refresh"
                  disabled={busy || !onUp}
                  onClick={onUp}
                  aria-label={`Move step ${image.sequence} up`}
                >
                  ↑ Up
                </button>
                <button
                  type="button"
                  className="btn-refresh"
                  disabled={busy || !onDown}
                  onClick={onDown}
                  aria-label={`Move step ${image.sequence} down`}
                >
                  ↓ Down
                </button>
                <button
                  type="button"
                  className="btn-refresh"
                  onClick={() => setEditing(true)}
                >
                  Edit text
                </button>
                <button
                  type="button"
                  className="btn-refresh"
                  disabled={replace.isPending}
                  onClick={() => fileRef.current?.click()}
                >
                  {replace.isPending ? "Replacing…" : "Replace image"}
                </button>
                <input
                  ref={fileRef}
                  type="file"
                  accept={ACCEPT}
                  className="sr-only"
                  aria-label={`Replacement image for step ${image.sequence}`}
                  onChange={(e) => {
                    const file = e.target.files?.[0];
                    e.target.value = "";
                    if (file) replace.mutate(file);
                  }}
                />
                <button
                  type="button"
                  className="btn-refresh text-danger"
                  disabled={busy}
                  onClick={onRemove}
                >
                  Remove
                </button>
              </>
            )}
          </div>
        )}
      </div>
    </li>
  );
}

function UploadForm({
  exerciseId,
  next,
  onDone,
}: {
  exerciseId: string;
  next: number;
  onDone: () => void;
}) {
  const { showToast } = useToast();
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<string | null>(null);
  const [alt, setAlt] = useState("");
  const [caption, setCaption] = useState("");
  const [source, setSource] = useState("");
  const [position, setPosition] = useState(String(next));
  const fileRef = useRef<HTMLInputElement>(null);
  const tooBig = !!file && file.size > MAX_MB * 1024 * 1024;
  const upload = useMutation({
    mutationFn: () =>
      uploadExerciseImage(exerciseId, {
        file: file!,
        altText: alt,
        caption,
        sourceAttribution: source,
        sequence: Number(position),
      }),
    onSuccess: () => {
      showToast("Image added.");
      if (preview) URL.revokeObjectURL(preview);
      setFile(null);
      setPreview(null);
      setAlt("");
      setCaption("");
      if (fileRef.current) fileRef.current.value = "";
      onDone();
    },
  });
  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (file && alt.trim() && !tooBig) upload.mutate();
  };

  return (
    <form
      onSubmit={onSubmit}
      aria-label="Add an image"
      className="space-y-3 rounded-md border border-dashed border-border p-3"
    >
      <h3 className="font-bold text-[#333]">Add an image</h3>
      <div>
        <label htmlFor="ex-upload-file" className="field-label">
          Image file
        </label>
        <input
          id="ex-upload-file"
          ref={fileRef}
          type="file"
          accept={ACCEPT}
          className="block w-full"
          onChange={(e) => {
            const f = e.target.files?.[0] ?? null;
            if (preview) URL.revokeObjectURL(preview);
            setFile(f);
            setPreview(f ? URL.createObjectURL(f) : null);
          }}
        />
        {tooBig && (
          <p className="mt-1 text-danger">This file is over {MAX_MB} MB.</p>
        )}
      </div>
      {preview && (
        <img
          src={preview}
          alt="Preview of the selected image"
          className="max-h-56 rounded border border-border object-contain"
        />
      )}
      <TextField
        id="ex-upload-alt"
        label="Description for screen readers (required)"
        value={alt}
        onChange={setAlt}
        help="Describe what the image shows, e.g. “Person lying on back, knees bent, hips lifted.”"
      />
      <div className="grid gap-3 sm:grid-cols-3">
        <TextField
          id="ex-upload-caption"
          label="Step caption"
          value={caption}
          onChange={setCaption}
          help="e.g. Starting position"
        />
        <TextField
          id="ex-upload-source"
          label="Source / licence"
          value={source}
          onChange={setSource}
          help="e.g. Drawn by clinic staff"
        />
        <div>
          <label htmlFor="ex-upload-position" className="field-label">
            Step number
          </label>
          <select
            id="ex-upload-position"
            className="field-input"
            value={position}
            onChange={(e) => setPosition(e.target.value)}
          >
            {Array.from({ length: next }, (_, i) => i + 1).map((n) => (
              <option key={n} value={n}>
                {n === next ? `${n} (last)` : n}
              </option>
            ))}
          </select>
        </div>
      </div>
      {upload.isError && (
        <p role="alert" className="alert-error">
          {upload.error.message}
        </p>
      )}
      <button
        type="submit"
        className="btn-primary"
        disabled={!file || !alt.trim() || tooBig || upload.isPending}
      >
        {upload.isPending ? "Uploading…" : "Upload image"}
      </button>
    </form>
  );
}

function TextField({
  id,
  label,
  value,
  onChange,
  help,
}: {
  id: string;
  label: string;
  value: string;
  onChange: (v: string) => void;
  help?: string;
}) {
  return (
    <div>
      <label htmlFor={id} className="field-label">
        {label}
      </label>
      <input
        id={id}
        className="field-input"
        value={value}
        onChange={(e) => onChange(e.target.value)}
      />
      {help && <p className="mt-1 text-text-muted">{help}</p>}
    </div>
  );
}
