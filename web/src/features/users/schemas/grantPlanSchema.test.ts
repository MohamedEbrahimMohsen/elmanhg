import { describe, expect, it } from 'vitest';
import { grantPlanSchema } from './grantPlanSchema';

describe('grantPlanSchema', () => {
  it('accepts Base Termly', () => {
    expect(grantPlanSchema.parse({ plan: 'Base', period: 'Termly' })).toEqual({ plan: 'Base', period: 'Termly' });
  });

  it('requires a plan', () => {
    expect(grantPlanSchema.safeParse({ period: 'Monthly' }).error?.issues[0]?.message).toBe(
      'users:validation.planRequired',
    );
  });

  it('requires a period', () => {
    expect(grantPlanSchema.safeParse({ plan: 'Base', period: 'Weekly' }).error?.issues[0]?.message).toBe(
      'users:validation.periodRequired',
    );
  });
});
