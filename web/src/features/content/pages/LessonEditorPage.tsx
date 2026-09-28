import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { useGetLesson } from '@/shared/api/generated/lessons/lessons';
import { ContentErrorState } from '../components/ContentErrorState';
import { ContentListSkeleton } from '../components/ContentListSkeleton';
import { LessonEditorForm } from '../components/LessonEditorForm';
import { LessonStateBadge } from '../components/LessonStateBadge';

export interface LessonEditorPageProps {
  lessonId: string;
}

export function LessonEditorPage({ lessonId }: LessonEditorPageProps) {
  const { t } = useTranslation('content');
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
            <Link to="/admin/content" className="text-accent underline">
              {t('lessonEditor.contentLink')}
            </Link>
          </li>
          {data ? <li aria-current="page">{data.name}</li> : null}
        </ol>
      </nav>
      <div className="flex flex-wrap items-center gap-2">
        <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('lessonEditor.title')}</h1>
        {data ? <LessonStateBadge state={data.state} /> : null}
      </div>
      {renderEditor()}
    </section>
  );
}
