import { describe, expect, it } from 'vitest';
import { progressSearchSchema } from './progressSearchSchema';

describe('progressSearchSchema', () => {
  it('accepts Quiz and Exam kinds', () => {
    expect(progressSearchSchema.parse({ kind: 'Quiz' }).kind).toBe('Quiz');
    expect(progressSearchSchema.parse({ kind: 'Exam' }).kind).toBe('Exam');
  });

  it('drops an unknown kind', () => {
    expect(progressSearchSchema.parse({ kind: 'Essay' }).kind).toBeUndefined();
  });

  it('coerces a numeric page string', () => {
    expect(progressSearchSchema.parse({ page: '2' }).page).toBe(2);
  });

  it('drops a page below 1', () => {
    expect(progressSearchSchema.parse({ page: '0' }).page).toBeUndefined();
  });
});
