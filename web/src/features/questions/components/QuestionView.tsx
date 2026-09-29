import { useTranslation } from 'react-i18next';
import { RichTextViewer } from '@/features/content';
import { fillStemHtml, type ChoiceReview, type QuestionAnswer, type StudentQuestion } from '../api/studentQuestion';
import { ChoiceAnswerInputs } from './ChoiceAnswerInputs';
import { EssayAnswerInput } from './EssayAnswerInput';
import { TextAnswerInputs } from './TextAnswerInputs';

export interface QuestionViewProps {
  question: StudentQuestion;
  answer: QuestionAnswer;
  onAnswerChange: (answer: QuestionAnswer) => void;
  disabled?: boolean;
  review?: ChoiceReview | undefined;
}

export function QuestionView({ question, answer, onAnswerChange, disabled, review }: QuestionViewProps) {
  const { t } = useTranslation('questions');
  const stem =
    question.type === 'Fill'
      ? fillStemHtml(question.stem, question.blankIds, (index) => t('view.blankMarker', { number: index + 1 }))
      : question.stem;
  const otherInputs =
    question.type === 'Fill' || question.type === 'Short' ? (
      <TextAnswerInputs question={question} answer={answer} onAnswerChange={onAnswerChange} disabled={disabled} />
    ) : (
      <ChoiceAnswerInputs
        question={question}
        answer={answer}
        onAnswerChange={onAnswerChange}
        disabled={disabled}
        review={review}
      />
    );
  const inputs =
    question.type === 'Essay' ? (
      <EssayAnswerInput question={question} answer={answer} onAnswerChange={onAnswerChange} disabled={disabled} />
    ) : (
      otherInputs
    );

  return (
    <div className="flex flex-col gap-4">
      <div className="text-body font-semibold">
        <RichTextViewer html={stem} />
      </div>
      {inputs}
    </div>
  );
}
