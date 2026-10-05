import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import type { LessonDetailResult } from '@/shared/api/generated/model';

export interface QuestionImportHeaderProps {
  lesson: LessonDetailResult;
}

export function QuestionImportHeader({ lesson }: QuestionImportHeaderProps) {
  const { t } = useTranslation('questions');

  return (
    <>
      <nav aria-label={t('import.breadcrumb')}>
        <ol className="flex flex-wrap gap-2 text-caption text-text-muted">
          <li>
            <Link to="/admin/questions" className="text-accent-text underline">
              {t('editor.questionsLink')}
            </Link>
          </li>
          <li>
            <Link to="/admin/lesson/$lessonId" params={{ lessonId: lesson.id }} className="text-accent-text underline">
              {lesson.name}
            </Link>
          </li>
          <li aria-current="page">{t('import.title')}</li>
        </ol>
      </nav>
      <div className="flex flex-col gap-1">
        <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('import.title')}</h1>
        <p className="text-caption text-text-muted">{t('import.intro')}</p>
      </div>
    </>
  );
}
