import { setupServer } from 'msw/node';
import {
  getGetExamAttemptsMockHandler,
  getGetUnitExamAttemptsMockHandler,
} from '@/shared/api/generated/exams/exams.msw';
import { noExamAttempts } from '@/test/examFixtures';

export const server = setupServer(
  getGetExamAttemptsMockHandler(noExamAttempts()),
  getGetUnitExamAttemptsMockHandler(noExamAttempts()),
);
