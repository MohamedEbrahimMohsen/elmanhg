import { useTranslation } from 'react-i18next';
import { RichTextViewer } from '@/features/content';
import type { MathDraftOwner } from '@/features/mathSteps';
import { fillStemHtml, type ChoiceReview, type QuestionAnswer, type StudentQuestion } from '../api/studentQuestion';
import { AnswerInputs } from './AnswerInputs';

export interface QuestionViewProps {
  question: StudentQuestion;
  answer: QuestionAnswer;
  onAnswerChange: (answer: QuestionAnswer) => void;
  disabled?: boolean;
  review?: ChoiceReview | undefined;
  mathDraftOwner?: MathDraftOwner | undefined;
}

export function QuestionView({
  question,
  answer,
  onAnswerChange,
  disabled,
  review,
  mathDraftOwner,
}: QuestionViewProps) {
  const { t } = useTranslation('questions');
  const stem =
    question.type === 'Fill'
      ? fillStemHtml(question.stem, question.blankIds, (index) => t('view.blankMarker', { number: index + 1 }))
      : question.stem;

  return (
    <div className="flex flex-col gap-4">
      <div className="text-body font-semibold">
        <RichTextViewer html={stem} />
      </div>
      <AnswerInputs
        question={question}
        answer={answer}
        onAnswerChange={onAnswerChange}
        disabled={disabled}
        review={review}
        mathDraftOwner={mathDraftOwner}
      />
    </div>
  );
}
