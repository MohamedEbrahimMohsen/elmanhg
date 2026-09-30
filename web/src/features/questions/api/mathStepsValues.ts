import type { JsonElement, UpdateQuestionRequest } from '@/shared/api/generated/model';
import type { QuestionValues } from '../schemas/questionEditorSchema';
import { mathStepsSpecSchema } from '../schemas/questionContentSchemas';

export function readMathSteps(spec: JsonElement): Partial<QuestionValues> {
  const parsed = mathStepsSpecSchema.safeParse(spec);
  if (!parsed.success) {
    return {};
  }
  const answers = parsed.data.acceptedAnswers.map((latex) => ({ latex }));
  return {
    mathAnswers: answers.length === 0 ? [{ latex: '' }] : answers,
    mathForm: parsed.data.form ?? 'equivalent',
    mathTolerance: parsed.data.tolerance === undefined ? '' : String(parsed.data.tolerance),
    mathToleranceMode: parsed.data.toleranceMode ?? 'absolute',
  };
}

export function toMathStepsContent(values: QuestionValues): Pick<UpdateQuestionRequest, 'body' | 'gradingSpec'> {
  return {
    body: {},
    gradingSpec: {
      acceptedAnswers: values.mathAnswers.map((answer) => answer.latex.trim()),
      form: values.mathForm,
      ...(values.mathTolerance.trim() === ''
        ? {}
        : { tolerance: Number(values.mathTolerance), toleranceMode: values.mathToleranceMode }),
    },
  };
}
