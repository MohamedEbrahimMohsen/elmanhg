import { resolve } from 'node:path';
import { compressDirectory } from './compress.ts';

const written = compressDirectory(resolve(import.meta.dirname, '../../dist'));
console.log(`Precompressed ${String(written.length)} files (.br and .gz).`);
