import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetLesson } from '@/shared/api/generated/lessons/lessons';
import { QuestionImportForm } from '../components/QuestionImportForm';
import { QuestionImportHeader } from '../components/QuestionImportHeader';
import { QuestionImportReport } from '../components/QuestionImportReport';
import { QuestionImportSuccess } from '../components/QuestionImportSuccess';
import { QuestionImportTemplateCard } from '../components/QuestionImportTemplateCard';
import { useQuestionImport } from '../hooks/useQuestionImport';

export interface QuestionImportPageProps {
  lessonId: string;
}

export function QuestionImportPage({ lessonId }: QuestionImportPageProps) {
  const { t } = useTranslation('questions');
  const { data, error, isPending, isError, refetch } = useGetLesson(lessonId);
  const { preview, imported, check, confirm, isConfirming, reset } = useQuestionImport(lessonId);
  const [formKey, setFormKey] = useState(0);

  if (isPending) {
    return <ContentListSkeleton label={t('import.loading')} />;
  }
  if (isError) {
    return (
      <ContentErrorState
        title={t('import.lessonErrorTitle')}
        error={error}
        onRetry={() => {
          void refetch();
        }}
      />
    );
  }

  const renderResult = () => {
    if (imported) {
      return (
        <QuestionImportSuccess
          lessonId={lessonId}
          result={imported}
          onAnother={() => {
            reset();
            setFormKey((key) => key + 1);
          }}
        />
      );
    }
    if (preview) {
      return (
        <QuestionImportReport
          preview={preview}
          onConfirm={() => {
            void confirm();
          }}
          isConfirming={isConfirming}
        />
      );
    }
    return null;
  };

  return (
    <section className="flex flex-col gap-4">
      <QuestionImportHeader lesson={data} />
      <QuestionImportTemplateCard />
      <QuestionImportForm key={formKey} onCheck={check} onFileChange={reset} />
      {renderResult()}
    </section>
  );
}
