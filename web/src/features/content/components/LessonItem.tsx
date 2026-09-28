import { Link } from '@tanstack/react-router';
import { ChevronDown, ChevronUp } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { LessonResult } from '@/shared/api/generated/model';
import { Button } from '@/shared/ui/button';
import { targetPosition } from '../api/position';
import { useLessonMutations } from '../hooks/useLessonMutations';
import { LessonActions } from './LessonActions';
import { LessonStateBadge } from './LessonStateBadge';

export interface LessonItemProps {
  lesson: LessonResult;
  isFirst: boolean;
  isLast: boolean;
  position: number;
}

export function LessonItem({ lesson, isFirst, isLast, position }: LessonItemProps) {
  const { t } = useTranslation('content');
  const { move } = useLessonMutations();

  return (
    <li className="flex flex-col gap-2 rounded-md border border-border bg-surface p-3">
      <div className="flex flex-wrap items-center gap-2">
        <Link
          to="/admin/lesson/$lessonId"
          params={{ lessonId: lesson.id }}
          className="text-ui font-semibold text-accent underline"
        >
          {lesson.name}
        </Link>
        <LessonStateBadge state={lesson.state} />
      </div>
      <div className="flex flex-wrap items-center gap-2">
        <Button
          size="sm"
          variant="secondary"
          aria-label={t('actions.moveUp', { name: lesson.name })}
          disabled={isFirst}
          onClick={() => {
            move(lesson.id, targetPosition(position - 1, 'up'));
          }}
        >
          <ChevronUp aria-hidden className="size-4" />
        </Button>
        <Button
          size="sm"
          variant="secondary"
          aria-label={t('actions.moveDown', { name: lesson.name })}
          disabled={isLast}
          onClick={() => {
            move(lesson.id, targetPosition(position - 1, 'down'));
          }}
        >
          <ChevronDown aria-hidden className="size-4" />
        </Button>
        <LessonActions lessonId={lesson.id} name={lesson.name} state={lesson.state} />
      </div>
    </li>
  );
}
