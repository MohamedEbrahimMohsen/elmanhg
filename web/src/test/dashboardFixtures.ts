import {
  getGetDashboardAskTeacherMockHandler,
  getGetDashboardContentMockHandler,
  getGetDashboardFunnelMockHandler,
  getGetDashboardPaymentsMockHandler,
  getGetDashboardSolveRateMockHandler,
  getGetDashboardStudentsMockHandler,
  getGetDashboardSubscribersMockHandler,
  getGetDashboardSuccessRateMockHandler,
  getGetDashboardValidationMockHandler,
} from '@/shared/api/generated/dashboard/dashboard.msw';
import type {
  AskTeacherMetricsResult,
  ContentMetricsResult,
  FunnelMetricsResult,
  PaymentMetricsResult,
  SolveRateMetricsResult,
  StudentMetricsResult,
  SubjectResult,
  SubscriberMetricsResult,
  SuccessRateMetricsResult,
  ValidationMetricsResult,
} from '@/shared/api/generated/model';

export const dashboardPhysicsId = '11111111-1111-4111-8111-111111111111';

const electricityId = '22222222-2222-4222-8222-222222222222';

export const dashboardSubjects: SubjectResult[] = [{ id: dashboardPhysicsId, name: 'Physics', order: 1, unitCount: 2 }];

const range = { from: '2026-09-17', to: '2026-09-30', generatedAt: '2026-09-30T10:00:00Z' };

const egp = (amountMinor: number) => ({ amountMinor, currency: 'EGP' });

const day = (date: string, value: number) => ({ date, value });

const scoped = { ...range, subjectId: null };

function group(id: string, name: string, parentId: string | null, attempts: number, correct: number, rate: number) {
  return { id, name, parentId, attempts, correct, rate };
}

export function studentMetrics(overrides: Partial<StudentMetricsResult> = {}): StudentMetricsResult {
  const dailyActive = [day('2026-09-29', 300), day('2026-09-30', 310)];
  return {
    ...range,
    total: 1250,
    newInRange: 80,
    newThisWeek: 21,
    activeToday: 310,
    activeThisMonth: 940,
    dailyActive,
    ...overrides,
  };
}

export function subscriberMetrics(overrides: Partial<SubscriberMetricsResult> = {}): SubscriberMetricsResult {
  return {
    ...range,
    activeSubscriptions: 420,
    activeByPlan: [
      { plan: 'Base', count: 400 },
      { plan: 'AskTeacher', count: 20 },
    ],
    churnedInRange: 12,
    churnedThisMonth: 15,
    monthlyRecurringRevenue: egp(8358000),
    ...overrides,
  };
}

export function contentMetrics(overrides: Partial<ContentMetricsResult> = {}): ContentMetricsResult {
  const questionsByType: ContentMetricsResult['questionsByType'] = [
    { type: 'Mcq', count: 600 },
    { type: 'Multi', count: 100 },
    { type: 'TrueFalse', count: 90 },
    { type: 'Fill', count: 80 },
    { type: 'Short', count: 70 },
    { type: 'Essay', count: 25 },
  ];
  return {
    subjectId: null,
    subjects: 3,
    units: 12,
    lessonsDraft: 4,
    lessonsPublished: 40,
    lessonsArchived: 1,
    questionsPending: 55,
    questionsApproved: 900,
    questionsRejected: 7,
    questionsRetired: 3,
    questionsByType,
    servableTotal: 870,
    generatedAt: range.generatedAt,
    ...overrides,
  };
}

export function solveRateMetrics(overrides: Partial<SolveRateMetricsResult> = {}): SolveRateMetricsResult {
  const daily = [
    { date: '2026-09-29', attempts: 2600, activeStudents: 900 },
    { date: '2026-09-30', attempts: 2800, activeStudents: 900 },
  ];
  return {
    ...scoped,
    attempts: 5400,
    activeStudentDays: 1800,
    attemptsPerActiveStudentPerDay: 3.25,
    daily,
    ...overrides,
  };
}

export function successRateMetrics(overrides: Partial<SuccessRateMetricsResult> = {}): SuccessRateMetricsResult {
  return {
    ...scoped,
    attempts: 5400,
    correct: 4050,
    rate: 0.75,
    bySubject: [group(dashboardPhysicsId, 'Physics', null, 5400, 4050, 0.75)],
    byUnit: [group(electricityId, 'Electricity', dashboardPhysicsId, 3000, 2400, 0.8)],
    byLesson: [group('33333333-3333-4333-8333-333333333333', 'Current and resistance', electricityId, 1000, 900, 0.9)],
    ...overrides,
  };
}

export function validationMetrics(overrides: Partial<ValidationMetricsResult> = {}): ValidationMetricsResult {
  const teacherId = '44444444-4444-4444-8444-444444444444';
  return {
    ...scoped,
    pendingBacklog: 55,
    approved: 120,
    rejected: 9,
    medianSecondsToDecision: 5400,
    byTeacher: [{ teacherId, displayName: 'Mohamed Ali', approved: 100, rejected: 6 }],
    dailyDecisions: [day('2026-09-29', 60), day('2026-09-30', 69)],
    ...overrides,
  };
}

export function askTeacherMetrics(overrides: Partial<AskTeacherMetricsResult> = {}): AskTeacherMetricsResult {
  return {
    ...scoped,
    openThreads: 14,
    awaitingReply: 5,
    overdueNow: 2,
    slaBreaches: 3,
    replies: 40,
    repliedWithinSla: 37,
    slaComplianceRate: 0.925,
    medianReplySeconds: 150,
    ...overrides,
  };
}

export function paymentMetrics(overrides: Partial<PaymentMetricsResult> = {}): PaymentMetricsResult {
  return {
    ...range,
    succeeded: 45,
    failed: 3,
    refunds: 1,
    revenue: egp(895500),
    refunded: egp(19900),
    netRevenue: egp(875600),
    revenueByDay: [day('2026-09-29', 400000), day('2026-09-30', 495500)],
    ...overrides,
  };
}

export function funnelMetrics(overrides: Partial<FunnelMetricsResult> = {}): FunnelMetricsResult {
  return {
    ...range,
    steps: [
      { type: 'LandingViewed', visitors: 1000, conversionFromPrevious: null },
      { type: 'SignUpStarted', visitors: 300, conversionFromPrevious: 0.3 },
      { type: 'SignUpCompleted', visitors: 200, conversionFromPrevious: 0.6667 },
      { type: 'OnboardingCompleted', visitors: 180, conversionFromPrevious: 0.9 },
      { type: 'FirstQuizAnswered', visitors: 150, conversionFromPrevious: 0.8333 },
    ],
    completedJourneys: 150,
    medianLandingToFirstAnswerSeconds: 129600,
    ...overrides,
  };
}

export function dashboardHandlers() {
  return [
    getGetDashboardStudentsMockHandler(studentMetrics()),
    getGetDashboardSubscribersMockHandler(subscriberMetrics()),
    getGetDashboardContentMockHandler(contentMetrics()),
    getGetDashboardSolveRateMockHandler(solveRateMetrics()),
    getGetDashboardSuccessRateMockHandler(successRateMetrics()),
    getGetDashboardValidationMockHandler(validationMetrics()),
    getGetDashboardAskTeacherMockHandler(askTeacherMetrics()),
    getGetDashboardPaymentsMockHandler(paymentMetrics()),
    getGetDashboardFunnelMockHandler(funnelMetrics()),
  ];
}
