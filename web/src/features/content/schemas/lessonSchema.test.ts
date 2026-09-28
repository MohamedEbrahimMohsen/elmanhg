import { describe, expect, it } from 'vitest';
import { lessonSchema } from './lessonSchema';

const valid = {
  name: "Newton's laws",
  explanation: '<p>Force</p>',
  summary: '',
  videoUrl: 'https://example.com/video',
  objectives: [{ objectiveId: 'o1', text: 'State the first law' }],
};

describe('lessonSchema', () => {
  it('accepts a complete lesson', () => {
    expect(lessonSchema.safeParse(valid).success).toBe(true);
  });

  it('rejects an empty name with validation.required', () => {
    expect(lessonSchema.safeParse({ ...valid, name: '' }).error?.issues[0]?.message).toBe('validation.required');
  });

  it('rejects a whitespace-only name', () => {
    expect(lessonSchema.safeParse({ ...valid, name: '   ' }).error?.issues[0]?.message).toBe('validation.required');
  });

  it('accepts an empty video link', () => {
    expect(lessonSchema.safeParse({ ...valid, videoUrl: '' }).success).toBe(true);
  });

  it('rejects a non-http video link with validation.url', () => {
    expect(lessonSchema.safeParse({ ...valid, videoUrl: 'javascript:alert(1)' }).error?.issues[0]?.message).toBe(
      'validation.url',
    );
    expect(lessonSchema.safeParse({ ...valid, videoUrl: 'ftp://x' }).error?.issues[0]?.message).toBe('validation.url');
  });

  it('rejects an empty objective with validation.required', () => {
    const result = lessonSchema.safeParse({ ...valid, objectives: [{ objectiveId: null, text: ' ' }] });

    expect(result.error?.issues[0]?.message).toBe('validation.required');
  });
});
