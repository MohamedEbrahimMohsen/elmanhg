import type { Role, Session } from './sessionStore';

export const devSessions: Record<Role, Session> = {
  student: { userId: 's1', displayName: 'أحمد', role: 'student' },
  teacher: { userId: 't1', displayName: 'أ. محمد', role: 'teacher' },
  admin: { userId: 'a1', displayName: 'المدير', role: 'admin' },
};
