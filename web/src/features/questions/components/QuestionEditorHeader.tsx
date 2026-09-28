import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import type { LessonDetailResult, QuestionDetailResult } from '@/shared/api/generated/model';
import { formatNumber } from '@/shared/lib/format';
import { cn } from '@/shared/lib/utils';
import { QuestionStatusBadge } from './QuestionStatusBadge';

export interface QuestionEditorHeaderProps {
  lesson: LessonDetailResult;
  question?: QuestionDetailResult | undefined;
}

const noticeClassName = 'rounded-md border px-3.5 py-3 text-caption text-text';

export function QuestionEditorHeader({ lesson, question }: QuestionEditorHeaderProps) {
  const { t, i18n } = useTranslation('questions');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const title = question ? t('editor.editTitle') : t('editor.newTitle');

  return (
    <>
      <nav aria-label={t('editor.breadcrumb')}>
        <ol className="flex flex-wrap gap-2 text-caption text-text-muted">
          <li>
            <Link to="/admin/questions" className="text-accent underline">
              {t('editor.questionsLink')}
            </Link>
          </li>
          <li>
            <Link to="/admin/lesson/$lessonId" params={{ lessonId: lesson.id }} className="text-accent underline">
              {lesson.name}
            </Link>
          </li>
          <li aria-current="page">{title}</li>
        </ol>
      </nav>
      <div className="flex flex-wrap items-center gap-2">
        <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{title}</h1>
        {question ? (
          <>
            <QuestionStatusBadge status={question.validationStatus} />
            <span className="rounded-full bg-soft px-2.5 py-0.5 text-micro font-semibold text-text-muted">
              {t('editor.versionBadge', { version: formatNumber(Number(question.version), lng, 'latin') })}
            </span>
          </>
        ) : null}
      </div>
      {question?.validationStatus === 'Approved' ? (
        <p className={cn(noticeClassName, 'border-warning bg-warning-soft')}>{t('editor.approvedWarning')}</p>
      ) : null}
      {question?.validationStatus === 'Rejected' ? (
        <p className={cn(noticeClassName, 'border-danger bg-danger-soft')}>
          {t('editor.rejectionReason', { reason: question.rejectionReason ?? '' })}
        </p>
      ) : null}
    </>
  );
}
