import { describe, expect, it, vi } from 'vitest';
import { createSignalRRealtimeClient } from './signalRRealtimeClient';

const hub = vi.hoisted(() => {
  const listeners = new Map<string, (payload: unknown) => void>();
  return {
    listeners,
    start: vi.fn(() => Promise.resolve()),
    stop: vi.fn(() => Promise.resolve()),
    build: vi.fn(),
  };
});

vi.mock('@microsoft/signalr', () => {
  class HubConnectionBuilder {
    withUrl() {
      return this;
    }
    withAutomaticReconnect() {
      return this;
    }
    configureLogging() {
      return this;
    }
    build() {
      hub.build();
      return {
        on: (event: string, listener: (payload: unknown) => void) => hub.listeners.set(event, listener),
        start: hub.start,
        stop: hub.stop,
      };
    }
  }
  return { HubConnectionBuilder, LogLevel: { None: 6 } };
});

describe('createSignalRRealtimeClient', () => {
  it('connects on start and routes hub events to the registered handlers', async () => {
    const client = createSignalRRealtimeClient();
    const handler = vi.fn();
    const off = client.on('teacherReplyReceived', handler);

    expect(hub.build).not.toHaveBeenCalled();
    await client.start();
    hub.listeners.get('teacherReplyReceived')?.({ threadId: 't1' });
    off();
    hub.listeners.get('teacherReplyReceived')?.({ threadId: 't2' });
    await client.stop();

    expect(hub.start).toHaveBeenCalledTimes(1);
    expect(hub.stop).toHaveBeenCalledTimes(1);
    expect(handler).toHaveBeenCalledTimes(1);
    expect(handler).toHaveBeenCalledWith({ threadId: 't1' });
  });

  it('swallows a failed connection because realtime is best effort', async () => {
    hub.start.mockRejectedValueOnce(new Error('offline'));
    const client = createSignalRRealtimeClient();

    await expect(client.start()).resolves.toBeUndefined();
  });
});
