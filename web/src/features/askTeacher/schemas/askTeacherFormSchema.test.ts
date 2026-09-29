import { describe, expect, it } from 'vitest';
import { askTeacherFormSchema } from './askTeacherFormSchema';

const lessonId = '11111111-1111-4111-8111-111111111111';

describe('askTeacherFormSchema', () => {
  it('accepts text when a context is attached', () => {
    expect(askTeacherFormSchema(false).safeParse({ text: 'Why?', lessonId: '', image: null }).success).toBe(true);
  });

  it('rejects blank text with the textRequired key', () => {
    const result = askTeacherFormSchema(false).safeParse({ text: '   ', lessonId: '', image: null });

    expect(result.error?.issues.map((issue) => issue.message)).toEqual(['askTeacher:form.textRequired']);
  });

  it('requires a lesson in picker mode', () => {
    const result = askTeacherFormSchema(true).safeParse({ text: 'Why?', lessonId: '', image: null });

    expect(result.error?.issues.map((issue) => issue.message)).toEqual(['askTeacher:form.lessonRequired']);
  });

  it('accepts a picked lesson in picker mode', () => {
    const photo = new File(['png'], 'photo.png', { type: 'image/png' });

    expect(askTeacherFormSchema(true).safeParse({ text: 'Why?', lessonId, image: photo }).success).toBe(true);
  });
});
