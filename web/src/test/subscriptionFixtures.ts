import type {
  EntitlementResult,
  PageDataOfPaymentResult,
  PaymentResult,
  PlanCatalogueResult,
  SubscriptionResult,
  SubscriptionStatus,
} from '@/shared/api/generated/model';

export const baseSubscriptionId = 'b1b1b1b1-b1b1-4b1b-8b1b-b1b1b1b1b1b1';
export const askTeacherSubscriptionId = 'a2a2a2a2-a2a2-4a2a-8a2a-a2a2a2a2a2a2';
export const paymentId = 'c3c3c3c3-c3c3-4c3c-8c3c-c3c3c3c3c3c3';

export function planCatalogue(): PlanCatalogueResult {
  return {
    free: { dailyQuizQuestions: 10, dailyAvatarMessages: 5, openLessonsPerUnit: 1 },
    base: {
      dailyAvatarMessages: 50,
      prices: [
        { period: 'Monthly', months: 1, price: { amountMinor: 19900, currency: 'EGP' } },
        { period: 'Termly', months: 4, price: { amountMinor: 69900, currency: 'EGP' } },
        { period: 'Yearly', months: 12, price: { amountMinor: 179900, currency: 'EGP' } },
      ],
    },
    askTeacher: {
      monthlyQuestions: 20,
      replySlaHours: 24,
      prices: [{ period: 'Monthly', months: 1, price: { amountMinor: 9900, currency: 'EGP' } }],
    },
  };
}

export function freeEntitlement(): EntitlementResult {
  return {
    tier: 'Free',
    hasAskTeacher: false,
    canTakeExams: false,
    dailyQuizQuestionLimit: 10,
    dailyAvatarMessageLimit: 5,
    openLessonsPerUnit: 1,
    monthlyAskTeacherQuestionLimit: 0,
    subscriptions: [],
  };
}

function subscription(id: string, plan: SubscriptionResult['plan'], status: SubscriptionStatus): SubscriptionResult {
  return {
    id,
    plan,
    period: 'Monthly',
    status,
    currentPeriodStart: '2026-09-29T12:00:00Z',
    currentPeriodEnd: '2026-10-29T12:00:00Z',
    entitledUntil: status === 'Active' || status === 'PastDue' ? '2026-11-01T12:00:00Z' : '2026-10-29T12:00:00Z',
    cancelledAt: status === 'Cancelled' ? '2026-10-01T12:00:00Z' : null,
  };
}

export function baseEntitlement({
  withAskTeacher = false,
  status = 'Active',
}: { withAskTeacher?: boolean; status?: SubscriptionStatus } = {}): EntitlementResult {
  const subscriptions = [subscription(baseSubscriptionId, 'Base', status)];
  if (withAskTeacher) {
    subscriptions.push(subscription(askTeacherSubscriptionId, 'AskTeacher', status));
  }
  return {
    tier: 'Base',
    hasAskTeacher: withAskTeacher,
    canTakeExams: true,
    dailyQuizQuestionLimit: null,
    dailyAvatarMessageLimit: 50,
    openLessonsPerUnit: null,
    monthlyAskTeacherQuestionLimit: withAskTeacher ? 20 : 0,
    subscriptions,
  };
}

export function payment(overrides: Partial<PaymentResult> = {}): PaymentResult {
  return {
    id: paymentId,
    plan: 'Base',
    period: 'Monthly',
    amount: { amountMinor: 19900, currency: 'EGP' },
    status: 'Succeeded',
    createdAt: '2026-09-29T11:59:00Z',
    completedAt: '2026-09-29T12:00:00Z',
    ...overrides,
  };
}

export function paymentsPage(
  items: PaymentResult[],
  { pageNumber = 1, totalPages = 1 }: { pageNumber?: number; totalPages?: number } = {},
): PageDataOfPaymentResult {
  return { items, pageNumber, pageSize: 10, totalItems: items.length, totalPages };
}
