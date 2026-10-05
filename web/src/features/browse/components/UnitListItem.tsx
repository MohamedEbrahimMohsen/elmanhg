import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { MasteryBar } from '@/features/mastery';
import type { StudentUnitSummaryResult } from '@/shared/api/generated/model';
import { Button } from '@/shared/ui/button';
import { circleBadgeClassName } from '@/shared/ui/circle';

export interface UnitListItemProps {
  unit: StudentUnitSummaryResult;
  ordinal: number;
}

export function UnitListItem({ unit, ordinal }: UnitListItemProps) {
  const { t } = useTranslation('browse');
  const percent = Number(unit.masteryPercent);

  return (
    <li className="flex items-start gap-3 rounded-md border border-border bg-surface px-3.5 py-3 shadow-1">
      <span aria-hidden="true" className={circleBadgeClassName}>
        {ordinal}
      </span>
      <div className="flex min-w-0 flex-1 flex-col gap-2">
        <Link
          to="/student/unit/$unitId"
          params={{ unitId: unit.id }}
          className="rounded-sm text-ui font-bold break-words text-text hover:text-accent-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
        >
          {unit.name}
        </Link>
        <MasteryBar percent={percent} label={t('mastery.barLabel', { name: unit.name })} />
        <p className="text-caption text-text-muted">
          {t('subject.unitMeta', { percent, lessons: Number(unit.lessonCount) })}
        </p>
        <p className="text-caption text-text-muted">
          {unit.bestExamScorePercent == null
            ? t('subject.noExam')
            : t('subject.bestExam', { score: Math.round(Number(unit.bestExamScorePercent)) })}
        </p>
        <Button asChild variant="secondary" className="self-start">
          <Link
            to="/student/exam-start/$unitId"
            params={{ unitId: unit.id }}
            aria-label={t('subject.unitExamLabel', { name: unit.name })}
          >
            {t('subject.unitExam')}
          </Link>
        </Button>
      </div>
    </li>
  );
}
