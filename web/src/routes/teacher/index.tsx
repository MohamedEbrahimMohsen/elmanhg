import { createFileRoute } from '@tanstack/react-router';
import { ValidationQueuePage, validationQueueSearchSchema } from '@/features/questions';

export const Route = createFileRoute('/teacher/')({
  validateSearch: validationQueueSearchSchema,
  component: ValidationQueuePage,
});
