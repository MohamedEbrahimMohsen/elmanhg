import { setupServer } from 'msw/node';
import { getRecordFunnelEventMockHandler } from '@/shared/api/generated/analytics/analytics.msw';
import { getGetAvatarStatusMockHandler } from '@/shared/api/generated/avatar/avatar.msw';
import {
  getGetStudentLessonMockHandler,
  getRecordLessonOpeningMockHandler,
} from '@/shared/api/generated/browse/browse.msw';
import { getReportClientErrorMockHandler } from '@/shared/api/generated/client-errors/client-errors.msw';
import {
  getGetExamAttemptsMockHandler,
  getGetUnitExamAttemptsMockHandler,
} from '@/shared/api/generated/exams/exams.msw';
import { getGetPaymentSettingsMockHandler } from '@/shared/api/generated/payments/payments.msw';
import { getGetSubjectsMockHandler } from '@/shared/api/generated/subjects/subjects.msw';
import { getGetMyUsageMockHandler } from '@/shared/api/generated/subscriptions/subscriptions.msw';
import { getGetTeacherInboxRemindersMockHandler } from '@/shared/api/generated/teacher-inbox/teacher-inbox.msw';
import { getGetTeacherReplyDeadlineMockHandler } from '@/shared/api/generated/teacher-threads/teacher-threads.msw';
import { teacherReplyDeadline } from '@/test/askTeacherFixtures';
import { avatarStatus } from '@/test/avatarFixtures';
import { studentLesson } from '@/test/browseFixtures';
import { dashboardHandlers } from '@/test/dashboardFixtures';
import { noExamAttempts } from '@/test/examFixtures';
import { paymentSettings } from '@/test/paymentFixtures';
import { baseUsage } from '@/test/subscriptionFixtures';

export const server = setupServer(
  getGetExamAttemptsMockHandler(noExamAttempts()),
  getGetUnitExamAttemptsMockHandler(noExamAttempts()),
  getGetStudentLessonMockHandler(studentLesson()),
  getRecordLessonOpeningMockHandler(),
  getRecordFunnelEventMockHandler(),
  getReportClientErrorMockHandler(),
  getGetMyUsageMockHandler(baseUsage()),
  getGetAvatarStatusMockHandler(avatarStatus()),
  getGetTeacherInboxRemindersMockHandler([]),
  getGetTeacherReplyDeadlineMockHandler(teacherReplyDeadline()),
  ...dashboardHandlers(),
  getGetSubjectsMockHandler([]),
  getGetPaymentSettingsMockHandler(paymentSettings()),
);
