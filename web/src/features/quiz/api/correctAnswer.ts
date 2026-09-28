import {
  fillSpecSchema,
  mcqSpecSchema,
  multiSpecSchema,
  shortNumericSpecSchema,
  shortTextSpecSchema,
  trueFalseSpecSchema,
  type ChoiceReview,
  type StudentQuestion,
} from '@/features/questions';

export type CorrectAnswerView =
  | { kind: 'options'; options: { id: string; text: string }[] }
  | { kind: 'trueFalse'; value: boolean }
  | { kind: 'blanks'; answers: { id: string; text: string }[] }
  | { kind: 'numeric'; value: number; tolerance: number; toleranceMode: 'absolute' | 'percent' }
  | { kind: 'text'; text: string };

function describeShort(question: StudentQuestion, correctAnswer: unknown): CorrectAnswerView | null {
  if (question.answerKind === 'numeric') {
    const spec = shortNumericSpecSchema.safeParse(correctAnswer);
    return spec.success ? { kind: 'numeric', ...spec.data } : null;
  }
  const spec = shortTextSpecSchema.safeParse(correctAnswer);
  return spec.success ? { kind: 'text', text: spec.data.acceptedAnswers[0] ?? '' } : null;
}

export function describeCorrectAnswer(question: StudentQuestion, correctAnswer: unknown): CorrectAnswerView | null {
  switch (question.type) {
    case 'Mcq': {
      const spec = mcqSpecSchema.safeParse(correctAnswer);
      return spec.success
        ? { kind: 'options', options: question.options.filter((option) => option.id === spec.data.correctOptionId) }
        : null;
    }
    case 'Multi': {
      const spec = multiSpecSchema.safeParse(correctAnswer);
      return spec.success
        ? {
            kind: 'options',
            options: question.options.filter((option) => spec.data.correctOptionIds.includes(option.id)),
          }
        : null;
    }
    case 'TrueFalse': {
      const spec = trueFalseSpecSchema.safeParse(correctAnswer);
      return spec.success ? { kind: 'trueFalse', value: spec.data.correctAnswer } : null;
    }
    case 'Fill': {
      const spec = fillSpecSchema.safeParse(correctAnswer);
      return spec.success
        ? {
            kind: 'blanks',
            answers: question.blankIds.map((id) => ({
              id,
              text: spec.data.blanks.find((blank) => blank.id === id)?.acceptedAnswers[0] ?? '',
            })),
          }
        : null;
    }
    case 'Short':
      return describeShort(question, correctAnswer);
  }
}

export function choiceReview(question: StudentQuestion, correctAnswer: unknown): ChoiceReview | undefined {
  switch (question.type) {
    case 'Mcq': {
      const spec = mcqSpecSchema.safeParse(correctAnswer);
      return spec.success ? { correctKeys: [spec.data.correctOptionId] } : undefined;
    }
    case 'Multi': {
      const spec = multiSpecSchema.safeParse(correctAnswer);
      return spec.success ? { correctKeys: spec.data.correctOptionIds } : undefined;
    }
    case 'TrueFalse': {
      const spec = trueFalseSpecSchema.safeParse(correctAnswer);
      return spec.success ? { correctKeys: [String(spec.data.correctAnswer)] } : undefined;
    }
    case 'Fill':
    case 'Short':
      return undefined;
  }
}
