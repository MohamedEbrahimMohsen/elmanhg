import { lazy } from 'react';

export const TeacherReviewNote = lazy(async () => ({
  default: (await import('./TeacherReviewNoteContent')).TeacherReviewNoteContent,
}));
