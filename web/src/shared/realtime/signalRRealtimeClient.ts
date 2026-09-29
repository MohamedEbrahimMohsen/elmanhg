import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { getAccessToken } from '@/shared/lib/authToken';
import { resolveApiUrl } from '@/shared/lib/http';
import type { RealtimeClient } from './realtimeClient';

export function createSignalRRealtimeClient(): RealtimeClient {
  const connection = new HubConnectionBuilder()
    .withUrl(resolveApiUrl('/api/hubs/notifications'), { accessTokenFactory: () => getAccessToken() ?? '' })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.None)
    .build();

  return {
    on: (event, handler) => {
      connection.on(event, handler);
      return () => {
        connection.off(event, handler);
      };
    },
    // realtime is best effort: pages refetch when opened, so a failed connection only loses the live update
    start: () => connection.start().catch(() => undefined),
    stop: () => connection.stop(),
  };
}
