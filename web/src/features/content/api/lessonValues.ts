import type { LessonDetailResult, UpdateLessonRequest } from '@/shared/api/generated/model';
import type { LessonValues } from '../schemas/lessonSchema';

const videoProtocols = ['http:', 'https:'];

export function toLessonValues(lesson: LessonDetailResult): LessonValues {
  return {
    name: lesson.name,
    explanation: lesson.explanation,
    summary: lesson.summary,
    videoUrl: lesson.videoUrl ?? '',
    objectives: [...lesson.objectives]
      .sort((a, b) => Number(a.order) - Number(b.order))
      .map((x) => ({ objectiveId: x.id, text: x.text })),
  };
}

export function toUpdateLessonRequest(values: LessonValues): UpdateLessonRequest {
  return {
    name: values.name,
    explanation: values.explanation,
    summary: values.summary,
    videoUrl: values.videoUrl === '' ? null : values.videoUrl,
    objectives: values.objectives.map((x) => ({ id: x.objectiveId, text: x.text })),
  };
}

export function toSafeVideoUrl(value: string): string | null {
  return URL.canParse(value) && videoProtocols.includes(new URL(value).protocol) ? value : null;
}
