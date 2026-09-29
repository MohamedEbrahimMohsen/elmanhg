import { useState } from 'react';
import { act, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { FakeRealtimeHub } from '@/test/fakeRealtimeHub';
import { RealtimeContext } from './RealtimeContext';
import { useRealtimeEvents } from './useRealtimeEvents';

function Probe({ onEvent }: { onEvent: (payload: unknown) => void }) {
  const [count, setCount] = useState(0);
  useRealtimeEvents({
    teacherReplyReceived: (payload) => {
      onEvent(payload);
      setCount((value) => value + 1);
    },
  });
  return <p>{count}</p>;
}

function renderProbe(hub: FakeRealtimeHub, onEvent: (payload: unknown) => void = () => undefined) {
  return render(
    <RealtimeContext value={hub.factory}>
      <Probe onEvent={onEvent} />
    </RealtimeContext>,
  );
}

describe('useRealtimeEvents', () => {
  it('delivers events to the mounted component', () => {
    const hub = new FakeRealtimeHub();
    renderProbe(hub);

    act(() => {
      hub.emit('teacherReplyReceived', { threadId: 'x' });
      hub.emit('teacherReplyReceived', { threadId: 'y' });
    });

    expect(screen.getByText('2')).toBeInTheDocument();
  });

  it('stops delivering after unmount', () => {
    const hub = new FakeRealtimeHub();
    const detached = vi.fn();
    const { unmount } = renderProbe(hub, detached);
    unmount();

    act(() => {
      hub.emit('teacherReplyReceived', { threadId: 'x' });
    });
    renderProbe(hub);

    expect(screen.getByText('0')).toBeInTheDocument();
    expect(detached).not.toHaveBeenCalled();
  });
});
