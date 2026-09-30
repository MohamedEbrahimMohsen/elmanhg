import { mathAnswerMaxLength } from '../api/questionOptions';

export interface MathStepsRuleInput {
  mathAnswers: { latex: string }[];
  mathForm: string;
  mathTolerance: string;
}

type IssueSink = (path: (string | number)[], message: string) => void;

const errorKey = (key: string) => `questions:editor.errors.${key}`;

export function addMathStepsIssues(values: MathStepsRuleInput, issue: IssueSink): void {
  if (values.mathAnswers.length === 0) {
    issue(['mathAnswers'], errorKey('mathAnswersCount'));
  }
  values.mathAnswers.forEach((answer, index) => {
    const latex = answer.latex.trim();
    if (latex === '') {
      issue(['mathAnswers', index, 'latex'], 'validation.required');
    } else if (latex.length > mathAnswerMaxLength) {
      issue(['mathAnswers', index, 'latex'], errorKey('mathAnswerLength'));
    }
  });
  const tolerance = values.mathTolerance.trim();
  if (tolerance === '') {
    return;
  }
  if (!Number.isFinite(Number(tolerance)) || Number(tolerance) < 0) {
    issue(['mathTolerance'], errorKey('tolerance'));
  }
  if (values.mathForm !== 'equivalent') {
    issue(['mathTolerance'], errorKey('mathToleranceForm'));
  }
}
