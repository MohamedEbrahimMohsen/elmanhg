import { describe, expect, it } from 'vitest';
import { gradeReviewCommentMaxLength } from '../api/gradeReviewOptions';
import { gradeReviewFormSchema } from './gradeReviewFormSchema';

const schema = gradeReviewFormSchema(5);

function issues(values: { decision: 'Accepted' | 'Overridden'; score: string; comment: string }) {
  const result = schema.safeParse(values);
  return result.success
    ? []
    : result.error.issues.map((issue) => ({ path: issue.path.join('.'), message: issue.message }));
}

describe('gradeReviewFormSchema', () => {
  it('accepts an accept decision without a score', () => {
    expect(issues({ decision: 'Accepted', score: '', comment: '' })).toEqual([]);
  });

  it('requires a score to override', () => {
    expect(issues({ decision: 'Overridden', score: '  ', comment: 'Good.' })).toEqual([
      { path: 'score', message: 'form.scoreRequired' },
    ]);
  });

  it('rejects a score above the full marks', () => {
    expect(issues({ decision: 'Overridden', score: '5.5', comment: 'Good.' })).toEqual([
      { path: 'score', message: 'form.scoreRange' },
    ]);
  });

  it('rejects three decimals', () => {
    expect(issues({ decision: 'Overridden', score: '1.234', comment: 'Good.' })).toEqual([
      { path: 'score', message: 'form.scoreRange' },
    ]);
  });

  it('requires a comment to override', () => {
    expect(issues({ decision: 'Overridden', score: '4', comment: '   ' })).toEqual([
      { path: 'comment', message: 'form.commentRequired' },
    ]);
  });

  it('rejects a comment over the limit', () => {
    expect(issues({ decision: 'Accepted', score: '', comment: 'a'.repeat(gradeReviewCommentMaxLength + 1) })).toEqual([
      { path: 'comment', message: 'form.commentTooLong' },
    ]);
  });
});
