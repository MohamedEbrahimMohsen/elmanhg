import { keepPreviousData } from '@tanstack/react-query';
import { useGetQuestions } from '@/shared/api/generated/questions/questions';
import { toQuestionListPage, toQuestionListParams } from '../api/questionListParams';
import type { QuestionListSearch } from '../schemas/questionListSearchSchema';

export function useQuestionList(search: QuestionListSearch) {
  return useGetQuestions(toQuestionListParams(search), {
    query: { placeholderData: keepPreviousData, select: toQuestionListPage },
  });
}
