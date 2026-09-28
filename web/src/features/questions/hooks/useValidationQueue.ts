import { keepPreviousData } from '@tanstack/react-query';
import { useGetValidationQueue } from '@/shared/api/generated/validation-queue/validation-queue';
import { toValidationQueuePage, toValidationQueueParams } from '../api/validationQueueParams';
import type { ValidationQueueSearch } from '../schemas/validationQueueSearchSchema';

export function useValidationQueue(search: ValidationQueueSearch, reviewSessionId: string | undefined) {
  return useGetValidationQueue(toValidationQueueParams(search, reviewSessionId ?? ''), {
    query: { enabled: reviewSessionId !== undefined, placeholderData: keepPreviousData, select: toValidationQueuePage },
  });
}
