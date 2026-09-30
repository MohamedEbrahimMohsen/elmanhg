export const maxImageEdge = 1600;
export const webpQuality = 0.8;

const webpType = 'image/webp';
const gifType = 'image/gif';
const extensionPattern = /\.[^./]*$/;

export function fitWithin(width: number, height: number, maxEdge: number): { width: number; height: number } {
  const longest = Math.max(width, height);
  if (longest <= maxEdge) {
    return { width, height };
  }

  const scale = maxEdge / longest;
  return { width: Math.round(width * scale), height: Math.round(height * scale) };
}

export async function optimizeImage(file: File): Promise<File> {
  if (file.type === gifType || !('createImageBitmap' in globalThis) || !('OffscreenCanvas' in globalThis)) {
    return file;
  }

  try {
    const blob = await reencode(file);
    if (blob?.type !== webpType || blob.size >= file.size) {
      return file;
    }

    return new File([blob], `${file.name.replace(extensionPattern, '')}.webp`, { type: webpType });
  } catch {
    return file;
  }
}

async function reencode(file: File): Promise<Blob | null> {
  const bitmap = await createImageBitmap(file);
  const size = fitWithin(bitmap.width, bitmap.height, maxImageEdge);
  const canvas = new OffscreenCanvas(size.width, size.height);
  const context = canvas.getContext('2d');
  if (!context) {
    bitmap.close();
    return null;
  }

  context.drawImage(bitmap, 0, 0, size.width, size.height);
  bitmap.close();
  return canvas.convertToBlob({ type: webpType, quality: webpQuality });
}
