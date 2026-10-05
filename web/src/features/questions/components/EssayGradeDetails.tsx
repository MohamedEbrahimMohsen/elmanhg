import { useTranslation } from 'react-i18next';
import type { EssayGradeDetailResult } from '@/shared/api/generated/model';
import { formatNumber } from '@/shared/lib/format';
import { EssayCriteriaList } from './EssayCriteriaList';

export interface EssayGradeDetailsProps {
  essay: EssayGradeDetailResult;
}

const percent = 100;

export function EssayGradeDetails({ essay }: EssayGradeDetailsProps) {
  const { t, i18n } = useTranslation('questions');
  const lng = i18n.resolvedLanguage ?? i18n.language;

  return (
    <section aria-label={t('preview.essay.title')} className="flex flex-col gap-3">
      <EssayCriteriaList criteria={essay.criteria} />
      <div className="flex flex-col gap-1">
        <h3 className="text-ui font-bold text-text">{t('preview.essay.justification')}</h3>
        <p className="text-body">{essay.justification}</p>
      </div>
      <p className="text-caption text-text-muted">
        {t('preview.essay.confidence', {
          confidence: formatNumber(Math.round(Number(essay.confidence) * percent), lng),
        })}
      </p>
      <p dir="ltr" className="text-caption text-text-muted">
        {t('preview.essay.model', { model: essay.model, promptVersion: essay.promptVersion })}
      </p>
    </section>
  );
}
