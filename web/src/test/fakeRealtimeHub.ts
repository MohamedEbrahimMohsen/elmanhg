import type { RealtimeClientFactory, RealtimeEventName } from '@/shared/realtime/realtimeClient';

type Handler = (payload: unknown) => void;

export class FakeRealtimeHub {
  private readonly handlers = new Map<RealtimeEventName, Set<Handler>>();

  readonly factory: RealtimeClientFactory = () => {
    const own: [RealtimeEventName, Handler][] = [];
    return {
      on: (event, handler) => {
        this.subscribe(event, handler);
        own.push([event, handler]);
        return () => {
          this.handlers.get(event)?.delete(handler);
        };
      },
      start: () => Promise.resolve(),
      stop: () => {
        own.forEach(([event, handler]) => {
          this.handlers.get(event)?.delete(handler);
        });
        return Promise.resolve();
      },
    };
  };

  emit(event: RealtimeEventName, payload: unknown): void {
    [...(this.handlers.get(event) ?? [])].forEach((handler) => {
      handler(payload);
    });
  }

  private subscribe(event: RealtimeEventName, handler: Handler): void {
    const set = this.handlers.get(event) ?? new Set<Handler>();
    set.add(handler);
    this.handlers.set(event, set);
  }
}
