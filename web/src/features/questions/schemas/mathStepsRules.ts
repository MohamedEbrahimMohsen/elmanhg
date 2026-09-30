import {
  mathAnswerMaxLength,
  mathSolutionStepMaxLength,
  mathSolutionStepsMax,
  mathStepsWeightMax,
} from '../api/questionOptions';

export interface MathStepsRuleInput {
  mathAnswers: { latex: string }[];
  mathForm: string;
  mathTolerance: string;
  mathSolution: { latex: string }[];
  mathStepsWeight: string;
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
  addSolutionIssues(values, issue);
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

function addSolutionIssues(values: MathStepsRuleInput, issue: IssueSink): void {
  const weight = values.mathStepsWeight.trim();
  if (!/^\d+$/.test(weight) || Number(weight) > mathStepsWeightMax) {
    issue(['mathStepsWeight'], errorKey('mathStepsWeight'));
  }
  if (values.mathSolution.length > mathSolutionStepsMax) {
    issue(['mathSolution'], errorKey('mathSolutionCount'));
  }
  values.mathSolution.forEach((step, index) => {
    const latex = step.latex.trim();
    if (latex === '') {
      issue(['mathSolution', index, 'latex'], 'validation.required');
    } else if (latex.length > mathSolutionStepMaxLength) {
      issue(['mathSolution', index, 'latex'], errorKey('mathSolutionStepLength'));
    }
  });
  if (Number(weight) > 0 && values.mathSolution.length === 0) {
    issue(['mathSolution'], errorKey('mathSolutionRequired'));
  }
}
