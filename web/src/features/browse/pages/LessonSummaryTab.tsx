import { useTranslation } from 'react-i18next';
import { useGetStudentLesson } from '@/shared/api/generated/browse/browse';
import { LessonRichText } from '../components/LessonRichText';

export interface LessonSummaryTabProps {
  lessonId: string;
}

export function LessonSummaryTab({ lessonId }: LessonSummaryTabProps) {
  const { t } = useTranslation('browse');
  const { data } = useGetStudentLesson(lessonId);
  if (!data) {
    return null;
  }

  return <LessonRichText html={data.summary} emptyText={t('lesson.noSummary')} />;
}
