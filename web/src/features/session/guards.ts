import { redirect } from '@tanstack/react-router';
import type { Role, Session } from './sessionStore';

export const roleHome = { student: '/student', teacher: '/teacher', admin: '/admin' } as const satisfies Record<
  Role,
  string
>;

export function requireRole(session: Session | null, role: Role, href: string): void {
  if (session === null) {
    throw redirect({ to: '/login', search: { redirect: href } });
  }
  if (session.role !== role) {
    throw redirect({ to: roleHome[session.role] });
  }
}

export function redirectSignedIn(session: Session | null, redirectTo: string | undefined): void {
  if (session === null) {
    return;
  }
  throw redirectTo ? redirect({ href: redirectTo }) : redirect({ to: roleHome[session.role] });
}

export function redirectToHome(session: Session | null): never {
  throw session === null ? redirect({ to: '/login' }) : redirect({ to: roleHome[session.role] });
}
