import type { JsonElement } from '@/shared/api/generated/model';
import { toQuestionValues } from '../api/questionValues';
import { EssayRubricView } from './EssayRubricView';
import { MathAnswerRulesView } from './MathAnswerRulesView';

export interface GradingKeyViewProps {
  type: string;
  maxScore: number;
  body: JsonElement;
  gradingSpec: JsonElement;
}

export function GradingKeyView({ type, maxScore, body, gradingSpec }: GradingKeyViewProps) {
  const values = toQuestionValues({
    type,
    stem: '',
    explanation: '',
    difficulty: 'Medium',
    objectiveId: null,
    tags: [],
    maxScore,
    body,
    gradingSpec,
  });

  if (values.type === 'Essay') {
    return <EssayRubricView criteria={values.criteria} modelAnswers={values.modelAnswers} />;
  }
  if (values.type === 'MathSteps') {
    return (
      <MathAnswerRulesView
        answers={values.mathAnswers}
        form={values.mathForm}
        tolerance={values.mathTolerance}
        toleranceMode={values.mathToleranceMode}
        solution={values.mathSolution}
        stepsWeight={values.mathStepsWeight}
      />
    );
  }
  return null;
}
