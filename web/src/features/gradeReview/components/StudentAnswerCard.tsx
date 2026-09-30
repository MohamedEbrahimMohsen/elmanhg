import { useTranslation } from 'react-i18next';
import { z } from 'zod';
import { MathStepsReadOnly } from '@/features/questions';
import type { GradeReviewDetailResult } from '@/shared/api/generated/model';

export interface StudentAnswerCardProps {
  detail: GradeReviewDetailResult;
}

const essayAnswerSchema = z.object({ text: z.string() });
const mathAnswerSchema = z.object({ steps: z.array(z.string().nullable()), finalAnswer: z.string() });

const cardClassName = 'flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5';

export function StudentAnswerCard({ detail }: StudentAnswerCardProps) {
  const { t } = useTranslation('gradeReview');

  return (
    <section aria-label={t('detail.answer')} className={cardClassName}>
      <h2 className="font-display text-h3 font-semibold">{t('detail.answer')}</h2>
      {detail.kind === 'MathSteps' ? <MathAnswer detail={detail} /> : <EssayAnswer detail={detail} />}
    </section>
  );
}

function EssayAnswer({ detail }: StudentAnswerCardProps) {
  const text = essayAnswerSchema.safeParse(detail.answer).data?.text ?? '';
  return (
    <p dir="auto" className="text-ui whitespace-pre-wrap">
      {text}
    </p>
  );
}

function MathAnswer({ detail }: StudentAnswerCardProps) {
  const parsed = mathAnswerSchema.safeParse(detail.answer).data;
  const steps = (parsed?.steps ?? []).map((step) => step ?? '');
  return <MathStepsReadOnly solution={{ steps, finalAnswer: parsed?.finalAnswer ?? '' }} />;
}
