import { readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { generateTokensCss } from './generateTokensCss.ts';

const webRoot = resolve(import.meta.dirname, '../..');
const markdown = readFileSync(resolve(webRoot, '../.claude/design-system.md'), 'utf8');

writeFileSync(resolve(webRoot, 'src/styles/tokens.css'), generateTokensCss(markdown));
