import { describe, expect, it } from 'vitest';
import { subscriptionSearchSchema } from './subscriptionSearchSchema';

describe('subscriptionSearchSchema', () => {
  it('accepts a positive payments page', () => {
    expect(subscriptionSearchSchema.parse({ paymentsPage: '2' }).paymentsPage).toBe(2);
  });

  it('drops a non-positive or non-numeric payments page', () => {
    expect(subscriptionSearchSchema.parse({ paymentsPage: 0 }).paymentsPage).toBeUndefined();
    expect(subscriptionSearchSchema.parse({ paymentsPage: 'x' }).paymentsPage).toBeUndefined();
  });
});
