import ar from './i18n/ar.json';

export { roles, createSessionStore, type Role, type Session, type SessionStore } from './sessionStore';
export { SessionContext } from './SessionContext';
export { useSession, useSessionStore } from './hooks/useSession';
export { useSignOut } from './hooks/useSignOut';
export { roleHome, requireRole, redirectSignedIn, requireOnboarded } from './guards';
export { can, roleCapabilities, type Capability } from './permissions';
export { loginSearchSchema } from './schemas/loginSearchSchema';
export { egyptianMobilePattern } from './schemas/fields';
export { restoreSession, installAuthHandlers, startSession, clearSession, toSession } from './authSession';
export { LoginPage } from './pages/LoginPage';
export { SignUpPage } from './pages/SignUpPage';
export { AcceptInvitePage } from './pages/AcceptInvitePage';

export const sessionLocales = { ar, en: () => import('./i18n/en.json') };
