import { useTranslation } from 'react-i18next';
import { EssayCriteriaList, MathStepScoreList } from '@/features/questions';
import type { GradeReviewDetailResult } from '@/shared/api/generated/model';
import { formatNumber } from '@/shared/lib/format';

export interface AiGradeCardProps {
  detail: GradeReviewDetailResult;
}

const cardClassName = 'flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5';

export function AiGradeCard({ detail }: AiGradeCardProps) {
  const { t, i18n } = useTranslation('gradeReview');
  const lng = i18n.resolvedLanguage ?? i18n.language;
  const maxScore = formatNumber(Number(detail.maxScore), lng);

  return (
    <section aria-label={t('detail.ai')} className={cardClassName}>
      <h2 className="font-display text-h3 font-semibold">{t('detail.ai')}</h2>
      {detail.aiScore === null ? (
        <>
          <p className="text-ui text-text">{t('detail.noAiScore')}</p>
          <p className="text-caption text-text-muted">
            {t(`reasons.${detail.reviewReason}`, { defaultValue: detail.reviewReason })}
          </p>
        </>
      ) : (
        <>
          <p className="text-ui font-semibold text-text">
            {t('detail.aiScore', { score: formatNumber(Number(detail.aiScore), lng), maxScore })}
          </p>
          {detail.confidence === null ? null : (
            <p className="text-caption text-text-muted">
              {t('detail.confidence', {
                percent: formatNumber(Math.round(Number(detail.confidence) * 100), lng),
              })}
            </p>
          )}
          {detail.kind === 'Essay' ? <EssayCriteriaList criteria={detail.criteria} /> : null}
          {detail.steps.length > 0 ? <MathStepScoreList steps={detail.steps} /> : null}
        </>
      )}
      {detail.finalAnswerVerdict === null ? null : (
        <p className="text-ui text-text">
          {t('detail.verdict', {
            verdict: t(`verdicts.${detail.finalAnswerVerdict}`, { defaultValue: detail.finalAnswerVerdict }),
          })}
        </p>
      )}
      {detail.justification === null ? null : (
        <p dir="auto" className="text-ui text-text-muted">
          {detail.justification}
        </p>
      )}
    </section>
  );
}
