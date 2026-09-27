import { createContext } from 'react';
import type { SessionStore } from './sessionStore';

export const SessionContext = createContext<SessionStore | null>(null);
