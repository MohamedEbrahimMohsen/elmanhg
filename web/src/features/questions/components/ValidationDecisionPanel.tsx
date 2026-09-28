import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import type { ValidationQuestionDetailResult } from '@/shared/api/generated/model';
import { Form } from '@/shared/form/Form';
import { Button } from '@/shared/ui/button';
import { questionDifficulties, toQuestionDifficulty } from '../api/questionOptions';
import { useQuestionDecision } from '../hooks/useQuestionDecision';
import { approveQuestionSchema, type ApproveQuestionValues } from '../schemas/approveQuestionSchema';
import { rejectQuestionSchema, type RejectQuestionValues } from '../schemas/rejectQuestionSchema';
import { SelectField } from './SelectField';
import { TextAreaField } from './TextAreaField';

export interface ValidationDecisionPanelProps {
  question: ValidationQuestionDetailResult;
}

export function ValidationDecisionPanel({ question }: ValidationDecisionPanelProps) {
  const { t } = useTranslation('questions');
  const { approve, reject, isPending } = useQuestionDecision();
  const target = { questionId: question.id, version: Number(question.version) };
  const approveForm = useForm<ApproveQuestionValues>({
    resolver: zodResolver(approveQuestionSchema),
    defaultValues: { difficulty: toQuestionDifficulty(question.difficulty) },
  });
  const rejectForm = useForm<RejectQuestionValues>({
    resolver: zodResolver(rejectQuestionSchema),
    defaultValues: { reason: '' },
  });

  return (
    <section
      aria-label={t('validation.decision.title')}
      className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5"
    >
      <h2 className="font-display text-h3 font-semibold">{t('validation.decision.title')}</h2>
      <Form form={approveForm} onSubmit={(values) => approve({ ...target, difficulty: values.difficulty })}>
        <SelectField<ApproveQuestionValues>
          name="difficulty"
          label={t('validation.decision.difficulty')}
          options={questionDifficulties.map((value) => ({ value, label: t(`difficulties.${value}`) }))}
        />
        <div>
          <Button type="submit" variant="primary" disabled={isPending}>
            {t('validation.decision.approve')}
          </Button>
        </div>
      </Form>
      <Form form={rejectForm} onSubmit={(values) => reject({ ...target, reason: values.reason })}>
        <TextAreaField<RejectQuestionValues> name="reason" label={t('validation.decision.reason')} />
        <div>
          <Button type="submit" variant="danger" disabled={isPending}>
            {t('validation.decision.reject')}
          </Button>
        </div>
      </Form>
      <p className="text-caption text-text-muted">{t('validation.decision.noEditNote')}</p>
    </section>
  );
}
