// Charts from pictures (charts v2 stage 5): a picture (file or pasted from the clipboard) as RGBA pixels for the engine's
// `chartFromPicture`. Large pictures are scaled down (the scanner needs no more than about 1600 px).

/** Largest side the scanner gets. */
export const MAX_PICTURE_SIDE = 1600;

/** True for file names the picture import takes. */
export function isPictureName(name: string): boolean {
  return /\.(png|jpe?g|bmp|gif|webp)$/i.test(name);
}

/** Target size for a picture of w × h (scaled down to MAX_PICTURE_SIDE, never up). */
export function scaledSize(w: number, h: number, max = MAX_PICTURE_SIDE): { w: number; h: number } {
  const k = Math.min(1, max / Math.max(w, h, 1));
  return { w: Math.max(1, Math.round(w * k)), h: Math.max(1, Math.round(h * k)) };
}

/** Base64 of bytes, in chunks (large arrays overflow String.fromCharCode). */
export function toBase64(bytes: Uint8Array | Uint8ClampedArray): string {
  let s = "";
  const chunk = 0x8000;
  for (let i = 0; i < bytes.length; i += chunk) s += String.fromCharCode(...bytes.subarray(i, i + chunk));
  return btoa(s);
}

/** The picture as RGBA pixels on white (transparent areas become white). */
export async function pictureToRgba(blob: Blob): Promise<{ w: number; h: number; payload: string }> {
  const bitmap = await createImageBitmap(blob);
  const { w, h } = scaledSize(bitmap.width, bitmap.height);
  const canvas = document.createElement("canvas");
  canvas.width = w;
  canvas.height = h;
  const g = canvas.getContext("2d");
  if (!g) throw new Error("no 2d canvas");
  g.fillStyle = "#ffffff";
  g.fillRect(0, 0, w, h);
  g.drawImage(bitmap, 0, 0, w, h);
  bitmap.close?.();
  return { w, h, payload: toBase64(g.getImageData(0, 0, w, h).data) };
}
