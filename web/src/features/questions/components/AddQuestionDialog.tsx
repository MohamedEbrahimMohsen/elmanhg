import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';
import { Dialog, DialogContent } from '@/shared/ui/dialog';
import { useQuestionLessonPicker } from '../hooks/useQuestionLessonPicker';
import { QuestionLessonPicker } from './QuestionLessonPicker';

export interface AddQuestionDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function AddQuestionDialog({ open, onOpenChange }: AddQuestionDialogProps) {
  const { t } = useTranslation('questions');
  const picker = useQuestionLessonPicker();
  const { lessonId } = picker;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent title={t('list.add.title')}>
        <p className="text-ui text-text">{t('list.add.intro')}</p>
        <QuestionLessonPicker picker={picker} />
        <div className="flex flex-wrap justify-end gap-3">
          <Button
            variant="ghost"
            onClick={() => {
              onOpenChange(false);
            }}
          >
            {t('list.add.cancel')}
          </Button>
          {lessonId === '' ? (
            <>
              <Button variant="ghost" disabled>
                {t('list.add.import')}
              </Button>
              <Button variant="secondary" disabled>
                {t('list.add.new')}
              </Button>
            </>
          ) : (
            <>
              <Button asChild variant="ghost">
                <Link to="/admin/question/import/$lessonId" params={{ lessonId }}>
                  {t('list.add.import')}
                </Link>
              </Button>
              <Button asChild variant="secondary">
                <Link to="/admin/question/new/$lessonId" params={{ lessonId }}>
                  {t('list.add.new')}
                </Link>
              </Button>
            </>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}
