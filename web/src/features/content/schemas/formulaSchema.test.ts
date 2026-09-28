import { describe, expect, it } from 'vitest';
import { formulaSchema } from './formulaSchema';

describe('formulaSchema', () => {
  it('accepts LaTeX and trims it', () => {
    expect(formulaSchema.parse({ latex: '  a^2+b^2 ', block: false }).latex).toBe('a^2+b^2');
  });

  it('rejects empty LaTeX with validation.required', () => {
    expect(formulaSchema.safeParse({ latex: '  ', block: true }).error?.issues[0]?.message).toBe('validation.required');
  });
});
