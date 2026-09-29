import type { Role, Session } from '@/features/session';

export const testSessions: Record<Role, Session> = {
  student: { userId: 's1', displayName: 'أحمد', role: 'student', needsOnboarding: false },
  teacher: { userId: 't1', displayName: 'أ. محمد', role: 'teacher', needsOnboarding: false },
  admin: { userId: 'a1', displayName: 'المدير', role: 'admin', needsOnboarding: false },
};

export const onboardingStudent: Session = { ...testSessions.student, needsOnboarding: true };
