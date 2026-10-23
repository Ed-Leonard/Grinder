export function getImageDimensions(
  file: File,
): Promise<{ width: number; height: number }> {
  return new Promise((resolve, reject) => {
    const url = URL.createObjectURL(file);
    const img = new Image();
    img.onload = () => {
      resolve({ width: img.naturalWidth, height: img.naturalHeight });
      URL.revokeObjectURL(url);
    };
    img.onerror = () => {
      reject(new Error("Could not read image dimensions"));
      URL.revokeObjectURL(url);
    };
    img.src = url;
  });
}

export function getVideoDimensions(
  file: File,
): Promise<{ width: number; height: number }> {
  return new Promise((resolve, reject) => {
    const url = URL.createObjectURL(file);
    const video = document.createElement("video");
    video.preload = "metadata";
    video.muted = true;
    video.onloadedmetadata = () => {
      resolve({ width: video.videoWidth, height: video.videoHeight });
      URL.revokeObjectURL(url);
    };
    video.onerror = () => {
      reject(new Error("Could not read video dimensions"));
      URL.revokeObjectURL(url);
    };
    video.src = url;
  });
}

export type TileShape =
  | "square"
  | "wide"
  | "tall"
  | "bigTall"
  | "bigSquare"
  | "bigWide"
  | "vwide";

interface ShapeSpan {
  colSpan: number;
  rowSpan: number;
}

export const TILE_SHAPES: Record<TileShape, ShapeSpan> = {
  square: { colSpan: 1, rowSpan: 1 },
  wide: { colSpan: 2, rowSpan: 1 },
  tall: { colSpan: 1, rowSpan: 2 },
  bigTall: { colSpan: 2, rowSpan: 4 },
  vwide: { colSpan: 3, rowSpan: 1 },
  bigSquare: { colSpan: 2, rowSpan: 2 },
  bigWide: { colSpan: 4, rowSpan: 2 },
};

export function getTileShape(
  width?: number | null,
  height?: number | null,
  isFeatured?: boolean,
): TileShape {
  if (!width || !height) return "square";

  const ratio = width / height;
  if (ratio < 0.8)
    if (isFeatured) return "bigTall";
    else return "tall";
  if (ratio < 1.2)
    if (isFeatured) return "square";
    else return "bigSquare";
  if (ratio < 2.1)
    if (isFeatured) return "bigWide";
    else return "wide";
  return "vwide";
}
