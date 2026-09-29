import { readdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { extname, join } from 'node:path';
import { brotliCompressSync, constants, gzipSync } from 'node:zlib';

export const compressibleExtensions = ['.js', '.css', '.html', '.svg', '.json', '.txt'] as const;
export const minimumBytes = 1024;

const brotliQuality = 11;
const gzipLevel = 9;

export function shouldCompress(path: string, size: number): boolean {
  return size >= minimumBytes && (compressibleExtensions as readonly string[]).includes(extname(path).toLowerCase());
}

export function compressDirectory(directory: string): string[] {
  const written: string[] = [];
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) {
      written.push(...compressDirectory(path));
      continue;
    }

    if (shouldCompress(path, statSync(path).size)) {
      written.push(...compressFile(path));
    }
  }

  return written;
}

function compressFile(path: string): string[] {
  const original = readFileSync(path);
  const outputs = [
    {
      path: `${path}.br`,
      bytes: brotliCompressSync(original, {
        params: {
          [constants.BROTLI_PARAM_QUALITY]: brotliQuality,
          [constants.BROTLI_PARAM_MODE]: constants.BROTLI_MODE_TEXT,
        },
      }),
    },
    { path: `${path}.gz`, bytes: gzipSync(original, { level: gzipLevel }) },
  ];
  return outputs
    .filter((output) => output.bytes.length < original.length)
    .map((output) => {
      writeFileSync(output.path, output.bytes);
      return output.path;
    });
}
