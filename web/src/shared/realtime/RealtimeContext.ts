import { createContext } from 'react';
import { noopRealtimeClientFactory, type RealtimeClientFactory } from './realtimeClient';

export const RealtimeContext = createContext<RealtimeClientFactory>(noopRealtimeClientFactory);
