import ar from './i18n/ar.json';
import en from './i18n/en.json';

export { roles, createSessionStore, type Role, type Session, type SessionStore } from './sessionStore';
export { SessionContext } from './SessionContext';
export { useSession, useSessionStore } from './hooks/useSession';
export { useSignOut } from './hooks/useSignOut';
export { devSessions } from './devSessions';
export { roleHome, requireRole, redirectSignedIn, redirectToHome } from './guards';
export { loginSearchSchema } from './schemas/loginSearchSchema';
export { DevSignInPage } from './pages/DevSignInPage';

export const sessionLocales = { ar, en };
