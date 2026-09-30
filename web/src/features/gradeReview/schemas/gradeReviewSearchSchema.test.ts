import { describe, expect, it } from 'vitest';
import { gradeReviewSearchSchema } from './gradeReviewSearchSchema';

describe('gradeReviewSearchSchema', () => {
  it('defaults the kind to Essay when missing or invalid', () => {
    expect(gradeReviewSearchSchema.parse({}).kind).toBe('Essay');
    expect(gradeReviewSearchSchema.parse({ kind: 'Drawing' }).kind).toBe('Essay');
    expect(gradeReviewSearchSchema.parse({ kind: 'MathSteps' }).kind).toBe('MathSteps');
  });

  it('drops an invalid subject id', () => {
    expect(gradeReviewSearchSchema.parse({ subjectId: 'not-a-guid' }).subjectId).toBeUndefined();
    expect(gradeReviewSearchSchema.parse({ subjectId: '12121212-1212-4212-8212-121212121212' }).subjectId).toBe(
      '12121212-1212-4212-8212-121212121212',
    );
  });

  it('coerces the page', () => {
    expect(gradeReviewSearchSchema.parse({ page: '3' }).page).toBe(3);
    expect(gradeReviewSearchSchema.parse({ page: '0' }).page).toBeUndefined();
  });
});
