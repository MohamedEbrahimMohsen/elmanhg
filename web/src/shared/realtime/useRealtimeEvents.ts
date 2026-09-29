import { use, useEffect, useEffectEvent } from 'react';
import { realtimeEventNames, type RealtimeEventName } from './realtimeClient';
import { RealtimeContext } from './RealtimeContext';

export type RealtimeHandlers = Partial<Record<RealtimeEventName, (payload: unknown) => void>>;

export function useRealtimeEvents(handlers: RealtimeHandlers): void {
  const factory = use(RealtimeContext);
  const dispatch = useEffectEvent((event: RealtimeEventName, payload: unknown) => {
    handlers[event]?.(payload);
  });

  useEffect(() => {
    const client = factory();
    const offs = realtimeEventNames.map((event) =>
      client.on(event, (payload) => {
        dispatch(event, payload);
      }),
    );
    void client.start();
    return () => {
      offs.forEach((off) => {
        off();
      });
      void client.stop();
    };
  }, [factory]);
}
