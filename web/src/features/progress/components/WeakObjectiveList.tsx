import { useTranslation } from 'react-i18next';
import type { WeakObjectiveResult } from '@/shared/api/generated/model';
import { WeakSpotList } from './WeakSpotList';

export interface WeakObjectiveListProps {
  objectives: WeakObjectiveResult[];
}

export function WeakObjectiveList({ objectives }: WeakObjectiveListProps) {
  const { t } = useTranslation('progress');

  return (
    <WeakSpotList
      items={objectives.map((objective) => ({
        id: objective.objectiveId,
        title: objective.text,
        meta: t('weakSpots.objectiveLesson', { lesson: objective.lessonName, subject: objective.subjectName }),
        percent: Number(objective.masteryPercent),
        barLabel: t('weakSpots.barLabel', { name: objective.text }),
        lessonId: objective.lessonId,
      }))}
    />
  );
}
