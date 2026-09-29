import { describe, expect, it } from 'vitest';
import { paymentLogSearchSchema } from './paymentLogSearchSchema';

describe('paymentLogSearchSchema', () => {
  it('keeps valid search values', () => {
    expect(
      paymentLogSearchSchema.parse({
        page: '2',
        view: 'review',
        status: 'Refunded',
        plan: 'AskTeacher',
        reference: ' 192036465 ',
        studentId: 'a7a7a7a7-a7a7-4a7a-8a7a-a7a7a7a7a7a7',
        from: '2026-09-01',
        to: '2026-09-10',
      }),
    ).toEqual({
      page: 2,
      view: 'review',
      status: 'Refunded',
      plan: 'AskTeacher',
      reference: '192036465',
      studentId: 'a7a7a7a7-a7a7-4a7a-8a7a-a7a7a7a7a7a7',
      from: '2026-09-01',
      to: '2026-09-10',
    });
  });

  it('drops invalid values', () => {
    expect(
      paymentLogSearchSchema.parse({
        page: '0',
        view: 'archive',
        status: 'Lost',
        plan: 'Gold',
        reference: 'x'.repeat(101),
        studentId: 'not-a-guid',
        from: '2026-13-40',
        to: 'tomorrow',
      }),
    ).toEqual({
      page: undefined,
      view: undefined,
      status: undefined,
      plan: undefined,
      reference: undefined,
      studentId: undefined,
      from: undefined,
      to: undefined,
    });
  });
});
