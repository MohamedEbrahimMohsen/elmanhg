import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetLesson } from '@/shared/api/generated/lessons/lessons';
import { useGetQuestion } from '@/shared/api/generated/questions/questions';
import { QuestionEditorForm } from '../components/QuestionEditorForm';
import { QuestionEditorHeader } from '../components/QuestionEditorHeader';
import { registerQuestionsAdminLocales } from '../questionsAdminLocales';

registerQuestionsAdminLocales();

export interface QuestionEditorPageProps {
  questionId: string;
}

export function QuestionEditorPage({ questionId }: QuestionEditorPageProps) {
  const { t } = useTranslation('questions');
  const questionQuery = useGetQuestion(questionId);
  const lessonQuery = useGetLesson(questionQuery.data?.lessonId ?? '');

  if (questionQuery.isError) {
    return (
      <ContentErrorState
        title={t('editor.errorTitle')}
        error={questionQuery.error}
        onRetry={() => {
          void questionQuery.refetch();
        }}
      />
    );
  }
  if (lessonQuery.isError) {
    return (
      <ContentErrorState
        title={t('editor.lessonErrorTitle')}
        error={lessonQuery.error}
        onRetry={() => {
          void lessonQuery.refetch();
        }}
      />
    );
  }
  if (questionQuery.isPending || lessonQuery.isPending) {
    return <ContentListSkeleton label={t('editor.loading')} />;
  }

  const question = questionQuery.data;
  return (
    <section className="flex flex-col gap-4">
      <QuestionEditorHeader lesson={lessonQuery.data} question={question} />
      <QuestionEditorForm
        key={`${question.id}-${String(question.version)}-${question.validationStatus}`}
        lesson={lessonQuery.data}
        question={question}
      />
    </section>
  );
}
