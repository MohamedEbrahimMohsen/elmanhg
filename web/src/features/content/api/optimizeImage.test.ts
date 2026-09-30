import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fitWithin, maxImageEdge, optimizeImage } from './optimizeImage';

const canvasSizes: { width: number; height: number }[] = [];
let encoded = new Blob();

class FakeCanvas {
  readonly width: number;
  readonly height: number;

  constructor(width: number, height: number) {
    this.width = width;
    this.height = height;
    canvasSizes.push({ width, height });
  }

  getContext() {
    return { drawImage: vi.fn() };
  }

  convertToBlob() {
    return Promise.resolve(encoded);
  }
}

const photo = (type = 'image/png', name = 'photo.PNG') => new File([new Uint8Array(1000)], name, { type });

const bytes = (size: number, type: string) => new Blob([new Uint8Array(size)], { type });

const stubBrowser = () => {
  const createBitmap = vi.fn(() => Promise.resolve({ width: 4000, height: 3000, close: vi.fn() }));
  vi.stubGlobal('createImageBitmap', createBitmap);
  vi.stubGlobal('OffscreenCanvas', FakeCanvas);
  return createBitmap;
};

describe('optimizeImage', () => {
  beforeEach(() => {
    canvasSizes.length = 0;
    encoded = bytes(10, 'image/webp');
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('fits a large landscape image within the maximum edge keeping its ratio', () => {
    expect(fitWithin(4000, 3000, maxImageEdge)).toEqual({ width: 1600, height: 1200 });
  });

  it('keeps the size of an image already within the edge', () => {
    expect(fitWithin(800, 600, maxImageEdge)).toEqual({ width: 800, height: 600 });
  });

  it('returns the original file when the browser cannot re-encode images', async () => {
    const original = photo();

    expect(await optimizeImage(original)).toBe(original);
  });

  it('keeps a gif untouched', async () => {
    const createBitmap = stubBrowser();
    const original = photo('image/gif', 'loop.gif');

    expect(await optimizeImage(original)).toBe(original);
    expect(createBitmap).not.toHaveBeenCalled();
  });

  it('returns a smaller webp named after the original', async () => {
    stubBrowser();

    const optimized = await optimizeImage(photo());

    expect(optimized.name).toBe('photo.webp');
    expect(optimized.type).toBe('image/webp');
    expect(optimized.size).toBe(10);
    expect(canvasSizes).toEqual([{ width: 1600, height: 1200 }]);
  });

  it('returns the original when re-encoding is not smaller or not webp', async () => {
    stubBrowser();
    const original = photo();

    encoded = bytes(2000, 'image/webp');
    expect(await optimizeImage(original)).toBe(original);
    encoded = bytes(10, 'image/png');
    expect(await optimizeImage(original)).toBe(original);
  });
});
