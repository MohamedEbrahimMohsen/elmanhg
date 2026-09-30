export async function readImageSize(file: File): Promise<{ width: number; height: number }> {
  if (!('createImageBitmap' in globalThis)) {
    throw new Error('Image decoding is unavailable.');
  }
  const bitmap = await createImageBitmap(file);
  const { width, height } = bitmap;
  bitmap.close();
  return { width, height };
}
