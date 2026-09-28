import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetLesson } from '@/shared/api/generated/lessons/lessons';
import { QuestionEditorForm } from '../components/QuestionEditorForm';
import { QuestionEditorHeader } from '../components/QuestionEditorHeader';

export interface NewQuestionPageProps {
  lessonId: string;
}

export function NewQuestionPage({ lessonId }: NewQuestionPageProps) {
  const { t } = useTranslation('questions');
  const { data, error, isPending, isError, refetch } = useGetLesson(lessonId);

  if (isPending) {
    return <ContentListSkeleton label={t('editor.loading')} />;
  }
  if (isError) {
    return (
      <ContentErrorState
        title={t('editor.lessonErrorTitle')}
        error={error}
        onRetry={() => {
          void refetch();
        }}
      />
    );
  }

  return (
    <section className="flex flex-col gap-4">
      <QuestionEditorHeader lesson={data} />
      <QuestionEditorForm lesson={data} />
    </section>
  );
}
