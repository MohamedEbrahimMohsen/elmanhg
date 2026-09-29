import type { HubConnection } from '@microsoft/signalr';
import { getAccessToken } from '@/shared/lib/authToken';
import { resolveApiUrl } from '@/shared/lib/http';
import { realtimeEventNames, type RealtimeClient, type RealtimeEventName } from './realtimeClient';

type Handler = (payload: unknown) => void;

async function connect(dispatch: (event: RealtimeEventName, payload: unknown) => void): Promise<HubConnection> {
  const { HubConnectionBuilder, LogLevel } = await import('@microsoft/signalr');
  const connection = new HubConnectionBuilder()
    .withUrl(resolveApiUrl('/api/hubs/notifications'), { accessTokenFactory: () => getAccessToken() ?? '' })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.None)
    .build();
  realtimeEventNames.forEach((event) => {
    connection.on(event, (payload: unknown) => {
      dispatch(event, payload);
    });
  });
  return connection;
}

export function createSignalRRealtimeClient(): RealtimeClient {
  const handlers = new Map<RealtimeEventName, Set<Handler>>(realtimeEventNames.map((event) => [event, new Set()]));
  let connection: Promise<HubConnection> | undefined;

  return {
    on: (event, handler) => {
      handlers.get(event)?.add(handler);
      return () => {
        handlers.get(event)?.delete(handler);
      };
    },
    // the signalr chunk loads on start so it stays out of the entry bundle budget (docs/performance.md)
    start: () => {
      connection ??= connect((event, payload) => {
        handlers.get(event)?.forEach((handler) => {
          handler(payload);
        });
      });
      // realtime is best effort: pages refetch when opened, so a failed connection only loses the live update
      return connection.then((hub) => hub.start()).catch(() => undefined);
    },
    stop: () => (connection ? connection.then((hub) => hub.stop()).catch(() => undefined) : Promise.resolve()),
  };
}
