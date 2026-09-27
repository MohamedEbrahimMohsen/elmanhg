import { describe, expect, it, vi } from 'vitest';
import { notifyExpired, refreshOnce, registerAuthHandlers } from './authToken';

describe('authToken', () => {
  it('returns false when no handlers are registered', async () => {
    await expect(refreshOnce()).resolves.toBe(false);
  });

  it('shares one refresh between concurrent callers', async () => {
    const refresh = vi.fn(() => Promise.resolve(true));
    registerAuthHandlers({ refresh, onExpired: vi.fn() });

    const results = await Promise.all([refreshOnce(), refreshOnce()]);

    expect(results).toEqual([true, true]);
    expect(refresh).toHaveBeenCalledTimes(1);
  });

  it('calls onExpired when notified', () => {
    const onExpired = vi.fn();
    registerAuthHandlers({ refresh: () => Promise.resolve(false), onExpired });

    notifyExpired();

    expect(onExpired).toHaveBeenCalledOnce();
  });
});
