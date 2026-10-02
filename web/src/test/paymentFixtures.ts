import type {
  AdminPaymentResult,
  PageDataOfAdminPaymentResult,
  PaymentSettingsResult,
} from '@/shared/api/generated/model';

export const adminPaymentId = 'f6f6f6f6-f6f6-4f6f-8f6f-f6f6f6f6f6f6';
export const adminPaymentStudentId = 'a7a7a7a7-a7a7-4a7a-8a7a-a7a7a7a7a7a7';

export function adminPayment(overrides: Partial<AdminPaymentResult> = {}): AdminPaymentResult {
  return {
    id: adminPaymentId,
    studentId: adminPaymentStudentId,
    studentName: 'Mona Ali',
    studentContact: 'mona@example.test',
    plan: 'Base',
    period: 'Monthly',
    periodMonths: 1,
    amount: { amountMinor: 19900, currency: 'EGP' },
    status: 'Succeeded',
    paymobTransactionId: '192036465',
    subscriptionId: 'b1b1b1b1-b1b1-4b1b-8b1b-b1b1b1b1b1b1',
    createdAt: '2026-09-29T12:00:00Z',
    completedAt: '2026-09-29T12:01:00Z',
    reviewReason: null,
    needsReview: false,
    reviewResolvedAt: null,
    refundedAt: null,
    refundReason: null,
    refundTransactionId: null,
    canRefund: true,
    ...overrides,
  };
}

export function paymentLogPage(
  items: AdminPaymentResult[],
  pageNumber = 1,
  totalPages = 1,
): PageDataOfAdminPaymentResult {
  return { items, pageNumber, pageSize: 20, totalItems: items.length, totalPages };
}

export function paymentSettings(overrides: Partial<PaymentSettingsResult> = {}): PaymentSettingsResult {
  return { refundsEnabled: true, ...overrides };
}
