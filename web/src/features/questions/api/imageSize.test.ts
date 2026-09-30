import { afterEach, describe, expect, it, vi } from 'vitest';
import { readImageSize } from './imageSize';

describe('readImageSize', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('reads the pixel size and releases the bitmap', async () => {
    const close = vi.fn();
    vi.stubGlobal('createImageBitmap', vi.fn().mockResolvedValue({ width: 800, height: 600, close }));

    await expect(readImageSize(new File(['x'], 'cell.png', { type: 'image/png' }))).resolves.toEqual({
      width: 800,
      height: 600,
    });
    expect(close).toHaveBeenCalledOnce();
  });

  it('fails when the browser cannot decode images', async () => {
    vi.stubGlobal('createImageBitmap', undefined);
    Reflect.deleteProperty(globalThis, 'createImageBitmap');

    await expect(readImageSize(new File(['x'], 'cell.png'))).rejects.toThrow('Image decoding is unavailable.');
  });
});
