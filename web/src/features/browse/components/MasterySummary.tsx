import { useTranslation } from 'react-i18next';
import { MasteryBar } from '@/features/mastery';

export interface MasterySummaryProps {
  name: string;
  percent: number;
  servable: number;
  seen: number;
}

export function MasterySummary({ name, percent, servable, seen }: MasterySummaryProps) {
  const { t } = useTranslation('browse');

  return (
    <div className="flex flex-col gap-2">
      <p className="text-caption text-text-muted">{t('mastery.line', { percent, servable, seen })}</p>
      <MasteryBar percent={percent} label={t('mastery.barLabel', { name })} />
    </div>
  );
}
