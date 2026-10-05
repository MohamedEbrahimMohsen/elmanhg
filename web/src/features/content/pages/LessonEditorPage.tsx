import { Link, useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { useGetLesson } from '@/shared/api/generated/lessons/lessons';
import { Button } from '@/shared/ui/button';
import { ContentErrorState } from '../components/ContentErrorState';
import { ContentListSkeleton } from '../components/ContentListSkeleton';
import { LessonActions } from '../components/LessonActions';
import { LessonEditorForm } from '../components/LessonEditorForm';
import { LessonStateBadge } from '../components/LessonStateBadge';

export interface LessonEditorPageProps {
  lessonId: string;
}

export function LessonEditorPage({ lessonId }: LessonEditorPageProps) {
  const { t } = useTranslation('content');
  const navigate = useNavigate();
  const { data, error, isPending, isError, refetch } = useGetLesson(lessonId);

  const renderEditor = () => {
    if (isPending) {
      return <ContentListSkeleton label={t('lessonEditor.loading')} />;
    }
    if (isError) {
      return (
        <ContentErrorState
          title={t('lessonEditor.errorTitle')}
          error={error}
          onRetry={() => {
            void refetch();
          }}
        />
      );
    }
    return <LessonEditorForm key={data.id} lesson={data} />;
  };

  return (
    <section className="flex flex-col gap-4">
      <nav aria-label={t('lessonEditor.breadcrumb')}>
        <ol className="flex gap-2 text-caption text-text-muted">
          <li>
            <Link to="/admin/content" className="text-accent-text underline">
              {t('lessonEditor.contentLink')}
            </Link>
          </li>
          {data ? <li aria-current="page">{data.name}</li> : null}
        </ol>
      </nav>
      <div className="flex flex-wrap items-center gap-2">
        <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('lessonEditor.title')}</h1>
        {data ? <LessonStateBadge state={data.state} /> : null}
        {data ? (
          <LessonActions
            lessonId={data.id}
            name={data.name}
            state={data.state}
            onDeleted={() => {
              void navigate({ to: '/admin/content' });
            }}
          />
        ) : null}
      </div>
      {data ? (
        <div className="flex flex-wrap gap-2">
          <Button asChild variant="secondary" size="sm">
            <Link to="/admin/question/new/$lessonId" params={{ lessonId: data.id }}>
              {t('lessonEditor.newQuestion')}
            </Link>
          </Button>
          <Button asChild variant="secondary" size="sm">
            <Link to="/admin/question/import/$lessonId" params={{ lessonId: data.id }}>
              {t('lessonEditor.importQuestions')}
            </Link>
          </Button>
          <Button asChild variant="ghost" size="sm">
            <Link to="/admin/questions" search={{ lessonId: data.id }}>
              {t('lessonEditor.viewQuestions')}
            </Link>
          </Button>
        </div>
      ) : null}
      {renderEditor()}
    </section>
  );
}
