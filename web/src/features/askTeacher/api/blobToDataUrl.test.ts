import { describe, expect, it } from 'vitest';
import { blobToDataUrl } from './blobToDataUrl';

describe('blobToDataUrl', () => {
  it('returns a data URL for an allowed media type', async () => {
    const blob = new Blob([new Uint8Array([0x1a, 0x45, 0xdf, 0xa3])], { type: 'audio/webm' });

    await expect(blobToDataUrl(blob, 'audio/')).resolves.toBe('data:audio/webm;base64,GkXfow==');
  });

  it('rejects a blob of another media type', async () => {
    const blob = new Blob(['<svg/>'], { type: 'image/svg+xml' });

    await expect(blobToDataUrl(blob, 'audio/')).rejects.toThrow('Unexpected media type.');
  });
});
