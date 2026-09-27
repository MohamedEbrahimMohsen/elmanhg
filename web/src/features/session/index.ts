import ar from './i18n/ar.json';
import en from './i18n/en.json';

export { roles, createSessionStore, type Role, type Session, type SessionStore } from './sessionStore';
export { SessionContext } from './SessionContext';
export { useSession, useSessionStore } from './hooks/useSession';
export { useSignOut } from './hooks/useSignOut';
export { roleHome, requireRole, redirectSignedIn, redirectToHome } from './guards';
export { loginSearchSchema } from './schemas/loginSearchSchema';
export { restoreSession, installAuthHandlers, startSession, clearSession, toSession } from './authSession';
export { LoginPage } from './pages/LoginPage';
export { SignUpPage } from './pages/SignUpPage';

export const sessionLocales = { ar, en };
