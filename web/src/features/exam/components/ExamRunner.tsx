import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { AskAvatarButton } from '@/features/avatar';
import type { ExamSessionResult } from '@/shared/api/generated/model';
import { Button } from '@/shared/ui/button';
import { examUnitNames, isMultiUnitExam, sortedExamItems } from '../api/examSession';
import { useExamAnswers } from '../hooks/useExamAnswers';
import { useExamCountdown } from '../hooks/useExamCountdown';
import { useSubmitExam } from '../hooks/useSubmitExam';
import { ExamHeader } from './ExamHeader';
import { ExamQuestionCard } from './ExamQuestionCard';
import { ExamSubmitDialog } from './ExamSubmitDialog';

export interface ExamRunnerProps {
  session: ExamSessionResult;
  receivedAt: number;
}

export function ExamRunner({ session, receivedAt }: ExamRunnerProps) {
  const { t } = useTranslation('exam');
  const [confirming, setConfirming] = useState(false);
  const submitRef = useRef<() => void>(() => undefined);
  const answers = useExamAnswers(session, () => {
    submitRef.current();
  });
  const submitter = useSubmitExam(session.id, answers.flush);
  const remaining = useExamCountdown(session.deadline, session.serverNow, receivedAt, submitter.submit);
  const items = sortedExamItems(session);

  useEffect(() => {
    submitRef.current = submitter.submit;
  });

  return (
    <section className="flex flex-col gap-3">
      <ExamHeader
        title={t(isMultiUnitExam(session) ? 'exam.multiTitle' : 'exam.title', { unit: examUnitNames(session) })}
        remainingMilliseconds={remaining}
        status={answers.status}
        lastSavedAt={answers.lastSavedAt}
      />
      {remaining === 0 ? (
        <p role="status" className="text-ui font-semibold text-text">
          {t('exam.timeUp')}
        </p>
      ) : null}
      <div className="flex flex-col gap-3">
        {items.map((item) => (
          <ExamQuestionCard
            key={item.questionId}
            item={item}
            total={items.length}
            answer={answers.answerOf(item.questionId)}
            onChange={(answer) => {
              answers.change(item, answer);
            }}
            disabled={submitter.isPending}
          />
        ))}
      </div>
      <div className="sticky bottom-0 flex justify-end gap-3 rounded-md border border-border bg-surface p-3 shadow-1 lg:static">
        <AskAvatarButton context={{ entryPoint: 'Global', title: t('exam.avatarContext') }} />
        <Button
          variant="primary"
          disabled={submitter.isPending}
          onClick={() => {
            setConfirming(true);
          }}
        >
          {t('exam.submit')}
        </Button>
      </div>
      <ExamSubmitDialog
        open={confirming}
        unansweredCount={answers.unansweredCount}
        isPending={submitter.isPending}
        onOpenChange={setConfirming}
        onConfirm={submitter.submit}
      />
    </section>
  );
}
