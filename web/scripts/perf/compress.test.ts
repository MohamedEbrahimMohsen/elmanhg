// @vitest-environment node
import { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { randomBytes } from 'node:crypto';
import { brotliDecompressSync, gunzipSync } from 'node:zlib';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { compressDirectory } from './compress.ts';

let directory = '';

const write = (name: string, content: Buffer) => {
  const path = join(directory, name);
  writeFileSync(path, content);
  return path;
};

const repetitive = (size: number) =>
  Buffer.from('export const value = 1;\n'.repeat(Math.ceil(size / 24)).slice(0, size));

describe('compress', () => {
  beforeEach(() => {
    directory = mkdtempSync(join(tmpdir(), 'perf-'));
  });

  afterEach(() => {
    rmSync(directory, { recursive: true, force: true });
  });

  it('writes br and gz next to a compressible asset', () => {
    const original = repetitive(4096);
    const path = write('app.js', original);

    compressDirectory(directory);

    expect(brotliDecompressSync(readFileSync(`${path}.br`))).toEqual(original);
    expect(gunzipSync(readFileSync(`${path}.gz`))).toEqual(original);
  });

  it('skips files under 1 KB', () => {
    const path = write('small.js', repetitive(500));

    compressDirectory(directory);

    expect(existsSync(`${path}.br`)).toBe(false);
    expect(existsSync(`${path}.gz`)).toBe(false);
  });

  it('skips binary formats such as woff2 and png', () => {
    const font = write('font.woff2', repetitive(4096));
    const image = write('image.png', repetitive(4096));

    const written = compressDirectory(directory);

    expect(written).toEqual([]);
    expect([font, image].some((path) => existsSync(`${path}.br`) || existsSync(`${path}.gz`))).toBe(false);
  });

  it('does not write output that is not smaller', () => {
    const path = write('random.js', randomBytes(2048));

    compressDirectory(directory);

    expect(existsSync(`${path}.br`)).toBe(false);
  });
});
