// Keeps each String.fromCharCode spread well under the engine's argument limit.
const chunkSize = 0x8000;

export async function blobToDataUrl(blob: Blob, mediaPrefix: 'image/' | 'audio/'): Promise<string> {
  if (!blob.type.startsWith(mediaPrefix)) {
    throw new Error('Unexpected media type.');
  }
  const bytes = new Uint8Array(await blob.arrayBuffer());
  let binary = '';
  for (let index = 0; index < bytes.length; index += chunkSize) {
    binary += String.fromCharCode(...bytes.subarray(index, index + chunkSize));
  }
  return `data:${blob.type};base64,${btoa(binary)}`;
}
