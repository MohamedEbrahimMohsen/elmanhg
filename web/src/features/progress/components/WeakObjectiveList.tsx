import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { MasteryBar } from '@/features/mastery';
import type { WeakObjectiveResult } from '@/shared/api/generated/model';
import { Button } from '@/shared/ui/button';

export interface WeakObjectiveListProps {
  objectives: WeakObjectiveResult[];
}

export function WeakObjectiveList({ objectives }: WeakObjectiveListProps) {
  const { t } = useTranslation('progress');

  return (
    <ul className="flex flex-col gap-2">
      {objectives.map((objective) => {
        const percent = Number(objective.masteryPercent);
        return (
          <li
            key={objective.objectiveId}
            className="flex flex-col gap-1.5 rounded-md border border-border bg-surface px-3.5 py-3"
          >
            <p className="text-ui font-semibold">{objective.text}</p>
            <p className="text-caption text-text-muted">
              {t('weakSpots.objectiveLesson', { lesson: objective.lessonName, subject: objective.subjectName })}
            </p>
            <p className="text-caption text-text-muted">{t('weakSpots.mastery', { percent })}</p>
            <MasteryBar percent={percent} label={t('weakSpots.barLabel', { name: objective.text })} />
            <div>
              <Button asChild size="sm" variant="secondary">
                <Link to="/student/lesson/$lessonId/practice" params={{ lessonId: objective.lessonId }}>
                  {t('weakSpots.train')}
                </Link>
              </Button>
            </div>
          </li>
        );
      })}
    </ul>
  );
}
