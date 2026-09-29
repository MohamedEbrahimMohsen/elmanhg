export const realtimeEventNames = ['teacherReplyReceived', 'teacherThreadReminder'] as const;

export type RealtimeEventName = (typeof realtimeEventNames)[number];

export interface RealtimeClient {
  on(event: RealtimeEventName, handler: (payload: unknown) => void): () => void;
  start(): Promise<void>;
  stop(): Promise<void>;
}

export type RealtimeClientFactory = () => RealtimeClient;

export const noopRealtimeClientFactory: RealtimeClientFactory = () => ({
  on: () => () => undefined,
  start: () => Promise.resolve(),
  stop: () => Promise.resolve(),
});
