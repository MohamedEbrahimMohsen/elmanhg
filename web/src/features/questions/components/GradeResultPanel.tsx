import { CircleAlert, CircleCheck, CircleX } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { QuestionGradeResult } from '@/shared/api/generated/model';
import { formatNumber } from '@/shared/lib/format';
import { cn } from '@/shared/lib/utils';

export interface GradeResultPanelProps {
  result: QuestionGradeResult;
}

const verdicts = {
  Correct: {
    icon: CircleCheck,
    panel: 'border-success bg-success-soft',
    color: 'text-success-text',
    key: 'preview.correct',
  },
  Partial: {
    icon: CircleAlert,
    panel: 'border-warning bg-warning-soft',
    color: 'text-warning',
    key: 'preview.partial',
  },
  Incorrect: { icon: CircleX, panel: 'border-danger bg-danger-soft', color: 'text-danger', key: 'preview.incorrect' },
} as const;

function toVerdict(outcome: string): keyof typeof verdicts {
  return outcome === 'Correct' || outcome === 'Partial' ? outcome : 'Incorrect';
}

export function GradeResultPanel({ result }: GradeResultPanelProps) {
  const { t, i18n } = useTranslation('questions');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const verdict = verdicts[toVerdict(result.outcome)];
  const Icon = verdict.icon;

  return (
    <div role="status" className={cn('flex items-center gap-3 rounded-md border px-3.5 py-3', verdict.panel)}>
      <Icon aria-hidden className={cn('size-6.5 shrink-0', verdict.color)} />
      <div className="flex flex-col">
        <p className="text-ui font-semibold text-text">{t(verdict.key)}</p>
        <p className="text-caption text-text">
          {t('preview.score', {
            score: formatNumber(Number(result.score), lng),
            maxScore: formatNumber(Number(result.maxScore), lng),
          })}
        </p>
        {result.feedback ? <p className="text-caption text-text-muted">{result.feedback}</p> : null}
      </div>
    </div>
  );
}
