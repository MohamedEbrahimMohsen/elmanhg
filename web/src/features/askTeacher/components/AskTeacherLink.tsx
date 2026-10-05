import { Link } from '@tanstack/react-router';
import { MessageCircleQuestion } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';

export interface AskTeacherLinkProps {
  lessonId?: string;
  attemptId?: string;
}

export function AskTeacherLink({ lessonId, attemptId }: AskTeacherLinkProps) {
  const { t } = useTranslation('askTeacher');

  return (
    <Button asChild variant="secondary">
      <Link to="/student/ask-new" search={{ lessonId, attemptId }}>
        <MessageCircleQuestion aria-hidden strokeWidth={1.8} className="size-4" />
        {t('link')}
      </Link>
    </Button>
  );
}
