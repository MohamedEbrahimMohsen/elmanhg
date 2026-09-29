export const lessonTabs = [
  { key: 'explanation', to: '/student/lesson/$lessonId' },
  { key: 'objectives', to: '/student/lesson/$lessonId/objectives' },
  { key: 'summary', to: '/student/lesson/$lessonId/summary' },
  { key: 'practice', to: '/student/lesson/$lessonId/practice' },
] as const;
