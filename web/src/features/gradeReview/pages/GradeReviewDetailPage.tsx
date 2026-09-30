import { getRouteApi, Link, useNavigate } from '@tanstack/react-router';
import { ChevronLeft } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton, RichTextViewer } from '@/features/content';
import { GradingKeyView } from '@/features/questions';
import { useGetEssayGradeReview, useGetMathStepGradeReview } from '@/shared/api/generated/grade-reviews/grade-reviews';
import { NotFound } from '@/shared/components/NotFound';
import { ApiError } from '@/shared/lib/apiError';
import { kindFromSegment } from '../api/gradeReviewOptions';
import { AiGradeCard } from '../components/AiGradeCard';
import { GradeReviewForm } from '../components/GradeReviewForm';
import { ReviewedCard } from '../components/ReviewedCard';
import { StudentAnswerCard } from '../components/StudentAnswerCard';
import { useReviewGrade } from '../hooks/useReviewGrade';
import { registerGradeReviewLocales } from '../locales';

registerGradeReviewLocales();

const routeApi = getRouteApi('/teacher/grade/$subjectId/$kind/$gradeId');

const cardClassName = 'flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5';
const linkClassName =
  'inline-flex items-center gap-1 self-start text-ui font-semibold text-accent focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden';

export function GradeReviewDetailPage() {
  const { t } = useTranslation('gradeReview');
  const { subjectId, kind: segment, gradeId } = routeApi.useParams();
  const navigate = useNavigate();
  const kind = kindFromSegment(segment);
  const essay = useGetEssayGradeReview(subjectId, gradeId, { query: { enabled: kind === 'Essay' } });
  const mathSteps = useGetMathStepGradeReview(subjectId, gradeId, { query: { enabled: kind === 'MathSteps' } });
  const review = useReviewGrade(kind ?? 'Essay', subjectId, gradeId);

  if (kind === null) {
    return <NotFound />;
  }
  const query = kind === 'Essay' ? essay : mathSteps;
  const back = (
    <Link to="/teacher/grades" search={{ subjectId, kind }} className={linkClassName}>
      <ChevronLeft aria-hidden className="size-4 rtl:rotate-180" />
      {t('detail.back')}
    </Link>
  );

  if (query.isError) {
    if (query.error instanceof ApiError && query.error.status === 404) {
      return (
        <section className="flex flex-col gap-3">
          {back}
          <p role="alert" className="text-ui text-text">
            {t('detail.notFound')}
          </p>
        </section>
      );
    }
    return (
      <ContentErrorState
        title={t('detail.errorTitle')}
        error={query.error}
        onRetry={() => {
          void query.refetch();
        }}
      />
    );
  }
  if (query.isPending) {
    return <ContentListSkeleton label={t('detail.loading')} />;
  }

  const detail = query.data;
  return (
    <section className="flex flex-col gap-4">
      {back}
      <div className="flex flex-col gap-1">
        <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('detail.title')}</h1>
        <p className="text-caption text-text-muted">
          <span className="rounded-full bg-accent-soft px-2.5 py-0.5 text-micro font-semibold text-accent">
            {t(`reasons.${detail.reviewReason}`, { defaultValue: detail.reviewReason })}
          </span>{' '}
          {`${detail.unitName} › ${detail.lessonName}`}
        </p>
      </div>
      <section aria-label={t('detail.question')} className={cardClassName}>
        <h2 className="font-display text-h3 font-semibold">{t('detail.question')}</h2>
        <RichTextViewer html={detail.stem} />
      </section>
      <GradingKeyView
        type={detail.questionType}
        maxScore={Number(detail.maxScore)}
        body={detail.body}
        gradingSpec={detail.gradingSpec}
      />
      <StudentAnswerCard detail={detail} />
      <AiGradeCard detail={detail} />
      {detail.review ? (
        <ReviewedCard detail={detail} review={detail.review} />
      ) : (
        <GradeReviewForm
          detail={detail}
          isPending={review.isPending}
          onSubmit={async (values, form) => {
            if (await review.submit(values, form)) {
              await navigate({ to: '/teacher/grades', search: { subjectId, kind } });
            }
          }}
        />
      )}
    </section>
  );
}
