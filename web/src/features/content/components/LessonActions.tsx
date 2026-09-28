import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';
import { availableLessonActions, type LessonAction } from '../api/lessonLifecycle';
import { useLessonMutations } from '../hooks/useLessonMutations';

export interface LessonActionsProps {
  lessonId: string;
  name: string;
  state: string;
  onDeleted?: () => void;
}

export function LessonActions({ lessonId, name, state, onDeleted }: LessonActionsProps) {
  const { t } = useTranslation('content');
  const { publish, unpublish, archive, remove } = useLessonMutations();
  const [pending, setPending] = useState<LessonAction | null>(null);
  const perform: Record<LessonAction, () => void> = {
    publish: () => {
      publish(lessonId);
    },
    unpublish: () => {
      unpublish(lessonId);
    },
    archive: () => {
      archive(lessonId);
    },
    delete: () => {
      remove(lessonId, onDeleted);
    },
  };

  return (
    <div role="group" aria-label={t('lessonActions.label', { name })} className="flex flex-wrap items-center gap-2">
      {pending ? (
        <>
          <p className="text-ui text-text">{t(`lessonActions.confirm.${pending}`, { name })}</p>
          <Button
            size="sm"
            variant={pending === 'delete' || pending === 'archive' ? 'danger' : 'primary'}
            onClick={() => {
              perform[pending]();
              setPending(null);
            }}
          >
            {t(`lessonActions.confirmButton.${pending}`)}
          </Button>
          <Button
            size="sm"
            variant="secondary"
            onClick={() => {
              setPending(null);
            }}
          >
            {t('actions.cancel')}
          </Button>
        </>
      ) : (
        availableLessonActions(state).map((action) => (
          <Button
            key={action}
            size="sm"
            variant={action === 'delete' ? 'danger' : 'secondary'}
            onClick={() => {
              setPending(action);
            }}
          >
            {t(`lessonActions.${action}`)}
          </Button>
        ))
      )}
    </div>
  );
}
