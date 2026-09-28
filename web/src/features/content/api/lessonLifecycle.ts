export type LessonAction = 'publish' | 'unpublish' | 'archive' | 'delete';

const actionsByState: Partial<Record<string, LessonAction[]>> = {
  Draft: ['publish', 'delete'],
  Published: ['unpublish', 'archive'],
  Archived: ['publish', 'unpublish', 'delete'],
};

export function availableLessonActions(state: string): LessonAction[] {
  return actionsByState[state] ?? [];
}
