import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import type { ImportQuestionsResult } from '@/shared/api/generated/model';
import { Button } from '@/shared/ui/button';

export interface QuestionImportSuccessProps {
  lessonId: string;
  result: ImportQuestionsResult;
  onAnother: () => void;
}

export function QuestionImportSuccess({ lessonId, result, onAnother }: QuestionImportSuccessProps) {
  const { t } = useTranslation('questions');

  return (
    <div role="status" className="flex flex-col gap-2 rounded-md border border-success bg-success-soft px-3.5 py-3">
      <p className="text-ui text-text">{t('import.success.title', { count: Number(result.createdCount) })}</p>
      <div className="flex flex-wrap gap-2">
        <Button asChild variant="secondary" size="sm">
          <Link to="/admin/questions" search={{ lessonId }}>
            {t('import.success.viewQuestions')}
          </Link>
        </Button>
        <Button variant="ghost" size="sm" onClick={onAnother}>
          {t('import.success.another')}
        </Button>
      </div>
    </div>
  );
}
