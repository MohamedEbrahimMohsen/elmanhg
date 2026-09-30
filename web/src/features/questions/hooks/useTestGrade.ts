import type { QuestionGradeResult } from '@/shared/api/generated/model';
import { useGradeQuestionDraft } from '@/shared/api/generated/questions/questions';
import { ApiError, unhandledErrorCode } from '@/shared/lib/apiError';
import { toQuestionRequest } from '../api/questionValues';
import { toAnswerPayload, toStudentQuestion, type QuestionAnswer } from '../api/studentQuestion';
import type { QuestionValues } from '../schemas/questionEditorSchema';

export interface TestGrade {
  grade: (values: QuestionValues, answer: QuestionAnswer) => void;
  result: QuestionGradeResult | undefined;
  errorCode: string | null;
  isPending: boolean;
}

export function useTestGrade(lessonId: string): TestGrade {
  const mutation = useGradeQuestionDraft();
  const errorCode = mutation.error
    ? mutation.error instanceof ApiError
      ? mutation.error.code
      : unhandledErrorCode
    : null;

  return {
    grade: (values, answer) => {
      mutation.mutate({
        data: {
          ...toQuestionRequest(values),
          lessonId,
          answer: toAnswerPayload(toStudentQuestion(values), answer),
        },
      });
    },
    result: mutation.data,
    errorCode,
    isPending: mutation.isPending,
  };
}
