import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetTeacherThreadContext } from '@/shared/api/generated/teacher-threads/teacher-threads';
import type { AskTeacherNewSearch } from '../schemas/askTeacherNewSearchSchema';
import { ContextSummary } from './ContextSummary';

export interface AttachedContextProps {
  search: AskTeacherNewSearch;
}

export function AttachedContext({ search }: AttachedContextProps) {
  const { t } = useTranslation('askTeacher');
  const { data, error, isPending, isError, refetch } = useGetTeacherThreadContext({
    ...(search.lessonId === undefined ? {} : { lessonId: search.lessonId }),
    ...(search.questionId === undefined ? {} : { questionId: search.questionId }),
    ...(search.attemptId === undefined ? {} : { attemptId: search.attemptId }),
  });

  if (isPending) {
    return <ContentListSkeleton label={t('context.loading')} />;
  }
  if (isError) {
    return (
      <ContentErrorState
        title={t('context.errorTitle')}
        error={error}
        onRetry={() => {
          void refetch();
        }}
      />
    );
  }
  return <ContextSummary context={data} />;
}
