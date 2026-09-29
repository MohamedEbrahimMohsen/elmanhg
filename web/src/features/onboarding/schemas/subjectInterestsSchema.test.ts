import { describe, expect, it } from 'vitest';
import { subjectInterestsSchema } from './subjectInterestsSchema';

describe('subjectInterestsSchema', () => {
  it('accepts one chosen subject', () => {
    expect(subjectInterestsSchema.safeParse({ subjectIds: ['66666666-6666-4666-8666-666666666666'] }).success).toBe(
      true,
    );
  });

  it('rejects no subject', () => {
    expect(subjectInterestsSchema.safeParse({ subjectIds: [] }).error?.issues[0]?.message).toBe(
      'onboarding:form.subjectRequired',
    );
  });
});
