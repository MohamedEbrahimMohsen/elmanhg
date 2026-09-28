import { describe, expect, it } from 'vitest';
import { createTestQueryClient } from '@/test/renderWithProviders';
import { invalidateMastery } from './invalidateMastery';

describe('invalidateMastery', () => {
  it('marks mastery queries stale and leaves other queries fresh', async () => {
    const client = createTestQueryClient();
    client.setQueryData(['/api/mastery/overview'], { streakDays: 1 });
    client.setQueryData(['/api/mastery/subjects/x'], { name: 'Physics' });
    client.setQueryData(['/api/sessions/s'], { id: 's' });

    await invalidateMastery(client);

    expect(client.getQueryState(['/api/mastery/overview'])?.isInvalidated).toBe(true);
    expect(client.getQueryState(['/api/mastery/subjects/x'])?.isInvalidated).toBe(true);
    expect(client.getQueryState(['/api/sessions/s'])?.isInvalidated).toBe(false);
  });
});
