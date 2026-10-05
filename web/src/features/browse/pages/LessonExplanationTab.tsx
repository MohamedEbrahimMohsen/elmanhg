import { useTranslation } from 'react-i18next';
import { toSafeVideoUrl } from '@/features/content';
import { useGetStudentLesson } from '@/shared/api/generated/browse/browse';
import { LessonRichText } from '../components/LessonRichText';

export interface LessonExplanationTabProps {
  lessonId: string;
}

export function LessonExplanationTab({ lessonId }: LessonExplanationTabProps) {
  const { t } = useTranslation('browse');
  const { data } = useGetStudentLesson(lessonId);
  if (!data) {
    return null;
  }

  const videoUrl = toSafeVideoUrl(data.videoUrl ?? '');
  return (
    <div className="flex flex-col gap-3">
      <LessonRichText html={data.explanation} emptyText={t('lesson.noExplanation')} />
      {videoUrl ? (
        <a
          href={videoUrl}
          target="_blank"
          rel="noopener noreferrer"
          dir="ltr"
          className="self-start rounded-sm text-ui text-accent-text underline focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
        >
          {t('lesson.video')}
        </a>
      ) : null}
    </div>
  );
}
