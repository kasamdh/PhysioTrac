import type { BodyFinding } from "../types";

/** Body-chart geometry: each view is drawn from these shapes, and a tap
 * inside one tells us the region and side. The drawing is 160 x 360 units;
 * findings store their point as 0-1 of that, so they render at any size. */

export const VIEW_W = 160;
export const VIEW_H = 360;

// Mirrors PhysioTrac.Domain.Enums (serialized as numbers).
export const BodyView = { Front: 0, Back: 1 } as const;
export const BodySide = {
  Left: 0,
  Right: 1,
  Bilateral: 2,
  Midline: 3,
} as const;
export const BodySideLabels: Record<number, string> = {
  0: "Left",
  1: "Right",
  2: "Both sides",
  3: "Midline",
};
export const FindingTypeLabels: Record<number, string> = {
  0: "Pain",
  1: "Numbness",
  2: "Tingling",
  3: "Burning",
  4: "Swelling",
  5: "Tenderness",
  6: "Incision",
  7: "Scar",
  8: "Radiating",
  9: "Other",
};
/** One letter per finding type, shown inside the marker. */
export const FindingMarks: Record<number, string> = {
  0: "P",
  1: "N",
  2: "T",
  3: "B",
  4: "S",
  5: "Td",
  6: "I",
  7: "Sc",
  8: "R",
  9: "O",
};
export const FindingColors: Record<number, string> = {
  0: "#c62828",
  1: "#5e35b1",
  2: "#8e24aa",
  3: "#ef6c00",
  4: "#0277bd",
  5: "#ad1457",
  6: "#37474f",
  7: "#6d4c41",
  8: "#d84315",
  9: "#455a64",
};

export const RegionLabels: Record<string, string> = {
  head: "Head",
  neck: "Neck",
  shoulder: "Shoulder",
  upperArm: "Upper arm",
  elbow: "Elbow",
  forearm: "Forearm",
  wristHand: "Wrist / hand",
  chest: "Chest",
  abdomen: "Abdomen",
  upperBack: "Upper back",
  midBack: "Mid back",
  lowBack: "Low back",
  pelvis: "Pelvis / groin",
  buttock: "Buttock",
  hip: "Hip",
  thigh: "Thigh",
  knee: "Knee",
  lowerLeg: "Lower leg",
  calf: "Calf",
  ankleFoot: "Ankle / foot",
  other: "Other",
};

type Shape =
  | { kind: "ellipse"; cx: number; cy: number; rx: number; ry: number }
  | { kind: "rect"; x: number; y: number; w: number; h: number };

export interface Region {
  key: string;
  /** Fixed side for a limb; "byX" = decided by which half was tapped. */
  side: number | "byX";
  shape: Shape;
}

const e = (cx: number, cy: number, rx: number, ry: number): Shape => ({
  kind: "ellipse",
  cx,
  cy,
  rx,
  ry,
});
const r = (x: number, y: number, w: number, h: number): Shape => ({
  kind: "rect",
  x,
  y,
  w,
  h,
});

/** Paired limbs: viewer-left / viewer-right shapes. */
function pair(key: string, left: Shape, right: Shape, view: number): Region[] {
  // Front view: the viewer's left is the patient's right. Back view: same side.
  const viewerLeftSide =
    view === BodyView.Front ? BodySide.Right : BodySide.Left;
  const viewerRightSide =
    view === BodyView.Front ? BodySide.Left : BodySide.Right;
  return [
    { key, side: viewerLeftSide, shape: left },
    { key, side: viewerRightSide, shape: right },
  ];
}

function limbs(view: number, back: boolean): Region[] {
  return [
    ...pair("shoulder", e(50, 70, 14, 10), e(110, 70, 14, 10), view),
    ...pair("upperArm", r(34, 78, 15, 40), r(111, 78, 15, 40), view),
    ...pair("elbow", e(41, 124, 8, 7), e(119, 124, 8, 7), view),
    ...pair("forearm", r(29, 130, 14, 38), r(117, 130, 14, 38), view),
    ...pair("wristHand", e(34, 180, 9, 14), e(126, 180, 9, 14), view),
    ...pair("hip", r(56, 138, 10, 24), r(94, 138, 10, 24), view),
    ...pair("thigh", r(59, 162, 20, 62), r(81, 162, 20, 62), view),
    ...pair("knee", e(69, 233, 10, 10), e(91, 233, 10, 10), view),
    ...pair(
      back ? "calf" : "lowerLeg",
      r(61, 243, 16, 58),
      r(83, 243, 16, 58),
      view,
    ),
    ...pair("ankleFoot", e(68, 313, 10, 13), e(92, 313, 10, 13), view),
  ];
}

export const REGIONS: Record<number, Region[]> = {
  [BodyView.Front]: [
    { key: "head", side: BodySide.Midline, shape: e(80, 28, 18, 22) },
    { key: "neck", side: BodySide.Midline, shape: r(72, 48, 16, 13) },
    { key: "chest", side: "byX", shape: r(58, 61, 44, 41) },
    { key: "abdomen", side: "byX", shape: r(60, 102, 40, 36) },
    { key: "pelvis", side: "byX", shape: r(66, 138, 28, 24) },
    ...limbs(BodyView.Front, false),
  ],
  [BodyView.Back]: [
    { key: "head", side: BodySide.Midline, shape: e(80, 28, 18, 22) },
    { key: "neck", side: BodySide.Midline, shape: r(72, 48, 16, 13) },
    { key: "upperBack", side: "byX", shape: r(58, 61, 44, 28) },
    { key: "midBack", side: "byX", shape: r(59, 89, 42, 22) },
    { key: "lowBack", side: "byX", shape: r(60, 111, 40, 27) },
    { key: "buttock", side: "byX", shape: r(66, 138, 28, 24) },
    ...limbs(BodyView.Back, true),
  ],
};

/** The side of the patient a point in a midline region belongs to. */
export function sideForPoint(region: Region, view: number, x: number): number {
  if (region.side !== "byX") return region.side;
  const viewerLeft = x * VIEW_W < VIEW_W / 2;
  if (Math.abs(x * VIEW_W - VIEW_W / 2) < 4) return BodySide.Midline;
  if (view === BodyView.Front)
    return viewerLeft ? BodySide.Right : BodySide.Left;
  return viewerLeft ? BodySide.Left : BodySide.Right;
}

/** A default point for a region and side (used when adding from the list). */
export function centerOf(
  view: number,
  key: string,
  side: number,
): { x: number; y: number } {
  const candidates = REGIONS[view].filter((g) => g.key === key);
  const region = candidates.find((g) => g.side === side) ?? candidates[0];
  if (!region) return { x: 0.5, y: 0.5 };
  const s = region.shape;
  let cx = s.kind === "ellipse" ? s.cx : s.x + s.w / 2;
  const cy = s.kind === "ellipse" ? s.cy : s.y + s.h / 2;
  if (
    region.side === "byX" &&
    side !== BodySide.Midline &&
    side !== BodySide.Bilateral
  ) {
    const viewerLeft =
      view === BodyView.Front
        ? side === BodySide.Right
        : side === BodySide.Left;
    cx =
      s.kind === "rect"
        ? viewerLeft
          ? s.x + s.w / 4
          : s.x + (3 * s.w) / 4
        : cx;
  }
  return { x: cx / VIEW_W, y: cy / VIEW_H };
}

/** Regions offered for a view in the list editor. */
export function regionKeys(view: number): string[] {
  return [...new Set(REGIONS[view].map((g) => g.key)), "other"];
}

const VIEW_NAMES: Record<number, string> = { 0: "front", 1: "back" };

/** A finding in words (lists, print, screen readers). */
export function describe(f: BodyFinding) {
  const side = BodySideLabels[f.side].toLowerCase();
  const region = RegionLabels[f.region]?.toLowerCase() ?? f.region;
  return [
    `${FindingTypeLabels[f.findingType]}: ${side} ${region} (${VIEW_NAMES[f.view]})`,
    f.severity != null ? `severity ${f.severity}/10` : null,
    f.radiatesTo ? `radiating to ${f.radiatesTo}` : null,
    f.annotation,
    f.comment,
  ]
    .filter(Boolean)
    .join(", ");
}
