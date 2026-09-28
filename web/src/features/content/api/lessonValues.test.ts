import { describe, expect, it } from 'vitest';
import type { LessonDetailResult } from '@/shared/api/generated/model';
import { toLessonValues, toSafeVideoUrl, toUpdateLessonRequest } from './lessonValues';

const lesson: LessonDetailResult = {
  id: 'l1',
  unitId: 'u1',
  name: "Newton's laws",
  order: 1,
  state: 'Draft',
  explanation: '<p>Force</p>',
  summary: '<p>Summary</p>',
  videoUrl: null,
  objectives: [
    { id: 'o2', text: 'Apply F = ma', order: 2 },
    { id: 'o1', text: 'State the first law', order: 1 },
  ],
};

describe('lessonValues', () => {
  it('maps a lesson to form values in objective order', () => {
    expect(toLessonValues(lesson)).toEqual({
      name: "Newton's laws",
      explanation: '<p>Force</p>',
      summary: '<p>Summary</p>',
      videoUrl: '',
      objectives: [
        { objectiveId: 'o1', text: 'State the first law' },
        { objectiveId: 'o2', text: 'Apply F = ma' },
      ],
    });
  });

  it('maps form values to the update request', () => {
    const request = toUpdateLessonRequest({
      name: "Newton's laws",
      explanation: '<p>Force</p>',
      summary: '',
      videoUrl: '',
      objectives: [
        { objectiveId: 'o1', text: 'State the first law' },
        { objectiveId: null, text: 'Explain inertia' },
      ],
    });

    expect(request).toEqual({
      name: "Newton's laws",
      explanation: '<p>Force</p>',
      summary: '',
      videoUrl: null,
      objectives: [
        { id: 'o1', text: 'State the first law' },
        { id: null, text: 'Explain inertia' },
      ],
    });
  });

  it('accepts http and https video links', () => {
    expect(toSafeVideoUrl('https://example.com/video')).toBe('https://example.com/video');
    expect(toSafeVideoUrl('http://example.com/video')).toBe('http://example.com/video');
  });

  it('rejects javascript and malformed video links', () => {
    expect(toSafeVideoUrl('javascript:alert(1)')).toBeNull();
    expect(toSafeVideoUrl('not a link')).toBeNull();
  });
});
