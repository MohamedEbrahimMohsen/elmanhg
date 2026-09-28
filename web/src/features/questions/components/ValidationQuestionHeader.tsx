import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import type { ValidationQuestionDetailResult } from '@/shared/api/generated/model';
import { formatNumber } from '@/shared/lib/format';
import { QuestionStatusBadge } from './QuestionStatusBadge';

export interface ValidationQuestionHeaderProps {
  question: ValidationQuestionDetailResult;
}

const badgeClassName = 'rounded-full bg-soft px-2.5 py-0.5 text-micro font-semibold text-text-muted';

export function ValidationQuestionHeader({ question }: ValidationQuestionHeaderProps) {
  const { t, i18n } = useTranslation('questions');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const lastRejection = question.decisions.filter((decision) => decision.outcome === 'Rejected').at(-1);
  const meta = [
    `${question.subjectName} · ${question.unitName} › ${question.lessonName}`,
    t('validation.detail.lessonState', {
      state: t([`validation.detail.lessonStates.${question.lessonState}`, question.lessonState]),
    }),
    t('validation.detail.objective', { objective: question.objectiveText ?? t('validation.detail.noObjective') }),
    t('validation.detail.difficulty', { difficulty: t([`difficulties.${question.difficulty}`, question.difficulty]) }),
    t('validation.detail.maxScore', { score: formatNumber(Number(question.maxScore), lng, 'latin') }),
    ...(question.tags.length > 0 ? [t('validation.detail.tags', { tags: question.tags.join('، ') })] : []),
  ];

  return (
    <>
      <nav aria-label={t('validation.detail.breadcrumb')}>
        <ol className="flex flex-wrap gap-2 text-caption text-text-muted">
          <li>
            <Link to="/teacher" className="text-accent underline">
              {t('validation.detail.queueLink')}
            </Link>
          </li>
          <li aria-current="page">{t('validation.detail.title')}</li>
        </ol>
      </nav>
      <div className="flex flex-wrap items-center gap-2">
        <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('validation.detail.title')}</h1>
        <QuestionStatusBadge status={question.validationStatus} />
        <span className={badgeClassName}>
          {t('editor.versionBadge', { version: formatNumber(Number(question.version), lng, 'latin') })}
        </span>
        {question.retiredAt ? <span className={badgeClassName}>{t('validation.detail.retired')}</span> : null}
      </div>
      <ul className="flex flex-col gap-1 rounded-lg border border-border bg-surface p-4 text-caption text-text shadow-1 lg:p-5">
        {meta.map((line) => (
          <li key={line}>{line}</li>
        ))}
      </ul>
      {lastRejection ? (
        <p className="rounded-md border border-danger bg-danger-soft px-3.5 py-3 text-caption text-text">
          {t('validation.detail.previousRejection', { reason: lastRejection.reason ?? '' })}
        </p>
      ) : null}
    </>
  );
}
