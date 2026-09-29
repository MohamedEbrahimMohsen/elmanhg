import type { SubjectInterestsResult } from '@/shared/api/generated/model';
import { chemistryId, physicsId } from './masteryFixtures';

export function subjectInterests(overrides?: Partial<SubjectInterestsResult>): SubjectInterestsResult {
  return {
    needsOnboarding: true,
    subjects: [
      { subjectId: physicsId, name: 'Physics', isSelected: false },
      { subjectId: chemistryId, name: 'Chemistry', isSelected: false },
    ],
    ...overrides,
  };
}
