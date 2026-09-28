import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import type { SubjectMasteryResult } from '@/shared/api/generated/model';
import { MasteryBar } from './MasteryBar';

export interface SubjectMasteryCardProps {
  subject: SubjectMasteryResult;
}

export function SubjectMasteryCard({ subject }: SubjectMasteryCardProps) {
  const { t } = useTranslation('mastery');
  const id = useId();
  const percent = Number(subject.masteryPercent);

  return (
    <article
      aria-labelledby={id}
      className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5"
    >
      <h3 id={id} className="font-display text-h2 font-bold lg:text-h2-desktop">
        {subject.name}
      </h3>
      <MasteryBar percent={percent} label={t('subjects.barLabel', { name: subject.name })} />
      <p className="text-caption text-text-muted">
        {t('subjects.mastery', { percent })} · {t('subjects.available', { count: Number(subject.servableCount) })}
      </p>
    </article>
  );
}
