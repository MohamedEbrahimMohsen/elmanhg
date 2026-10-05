import { useState } from 'react';
import { Link } from '@tanstack/react-router';
import { Plus } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';
import { AddQuestionDialog } from './AddQuestionDialog';

export interface AddQuestionButtonProps {
  lessonId: string | undefined;
}

export function AddQuestionButton({ lessonId }: AddQuestionButtonProps) {
  const { t } = useTranslation('questions');
  const [open, setOpen] = useState(false);

  if (lessonId) {
    return (
      <Button asChild>
        <Link to="/admin/question/new/$lessonId" params={{ lessonId }}>
          <Plus aria-hidden className="size-4" />
          {t('list.add.button')}
        </Link>
      </Button>
    );
  }

  return (
    <>
      <Button
        aria-haspopup="dialog"
        onClick={() => {
          setOpen(true);
        }}
      >
        <Plus aria-hidden className="size-4" />
        {t('list.add.button')}
      </Button>
      <AddQuestionDialog open={open} onOpenChange={setOpen} />
    </>
  );
}
