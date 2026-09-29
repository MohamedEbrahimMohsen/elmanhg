import { useTranslation } from 'react-i18next';
import { useGetMyUsage } from '@/shared/api/generated/subscriptions/subscriptions';

export interface DailyQuizCounterProps {
  variant: 'practice' | 'quiz';
}

export function DailyQuizCounter({ variant }: DailyQuizCounterProps) {
  const { t } = useTranslation('subscription');
  const { data } = useGetMyUsage();
  if (data?.dailyQuizQuestionLimit == null) {
    return null;
  }

  return (
    <p className="text-caption text-text-muted">
      {t(`usage.${variant}Counter`, {
        used: Number(data.quizQuestionsUsedToday),
        limit: Number(data.dailyQuizQuestionLimit),
      })}
    </p>
  );
}
