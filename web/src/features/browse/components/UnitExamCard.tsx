import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import type { StudentUnitResult } from '@/shared/api/generated/model';
import { Button } from '@/shared/ui/button';

export interface UnitExamCardProps {
  unitId: string;
  bestExamScorePercent: StudentUnitResult['bestExamScorePercent'];
}

export function UnitExamCard({ unitId, bestExamScorePercent }: UnitExamCardProps) {
  const { t } = useTranslation('browse');

  return (
    <div className="flex flex-col items-start gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">
      <h2 className="font-display text-h2 font-bold lg:text-h2-desktop">{t('unit.examTitle')}</h2>
      <p className="text-ui text-text-muted">
        {bestExamScorePercent == null
          ? t('unit.noExam')
          : t('unit.bestExam', { score: Math.round(Number(bestExamScorePercent)) })}
      </p>
      <Button asChild variant="primary">
        <Link to="/student/exam-start/$unitId" params={{ unitId }}>
          {t('unit.openExam')}
        </Link>
      </Button>
    </div>
  );
}
