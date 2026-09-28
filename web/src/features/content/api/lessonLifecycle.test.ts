import { describe, expect, it } from 'vitest';
import { availableLessonActions } from './lessonLifecycle';

describe('availableLessonActions', () => {
  it('returns publish and delete for a draft', () => {
    expect(availableLessonActions('Draft')).toEqual(['publish', 'delete']);
  });

  it('returns unpublish and archive for a published lesson', () => {
    expect(availableLessonActions('Published')).toEqual(['unpublish', 'archive']);
  });

  it('returns publish, unpublish and delete for an archived lesson', () => {
    expect(availableLessonActions('Archived')).toEqual(['publish', 'unpublish', 'delete']);
  });

  it('returns no actions for an unknown state', () => {
    expect(availableLessonActions('Removed')).toEqual([]);
  });
});
