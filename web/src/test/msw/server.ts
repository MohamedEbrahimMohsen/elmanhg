import { setupServer } from 'msw/node';
import { getRecordFunnelEventMockHandler } from '@/shared/api/generated/analytics/analytics.msw';
import {
  getGetStudentLessonMockHandler,
  getRecordLessonOpeningMockHandler,
} from '@/shared/api/generated/browse/browse.msw';
import {
  getGetExamAttemptsMockHandler,
  getGetUnitExamAttemptsMockHandler,
} from '@/shared/api/generated/exams/exams.msw';
import { getGetMyUsageMockHandler } from '@/shared/api/generated/subscriptions/subscriptions.msw';
import { studentLesson } from '@/test/browseFixtures';
import { noExamAttempts } from '@/test/examFixtures';
import { baseUsage } from '@/test/subscriptionFixtures';

export const server = setupServer(
  getGetExamAttemptsMockHandler(noExamAttempts()),
  getGetUnitExamAttemptsMockHandler(noExamAttempts()),
  getGetStudentLessonMockHandler(studentLesson()),
  getRecordLessonOpeningMockHandler(),
  getRecordFunnelEventMockHandler(),
  getGetMyUsageMockHandler(baseUsage()),
);
