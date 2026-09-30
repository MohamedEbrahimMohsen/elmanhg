import { screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

async function openStats(lng: 'en' | 'ar' = 'en') {
  const rendered = renderApp('/teacher/stats', { session: testSessions.teacher, lng });
  await rendered.router.loadRouteChunk(rendered.router.routesById['/teacher/stats']);
  return rendered;
}

async function region(name: string) {
  return within(await screen.findByRole('region', { name }));
}

describe('TeacherStatsPage', () => {
  it('shows the page heading and my stats card', async () => {
    await openStats();

    expect(await screen.findByRole('heading', { level: 1, name: 'My stats' })).toBeInTheDocument();
    expect(await (await region('My reviews and replies')).findByText('Approved: 12')).toBeInTheDocument();
    expect(screen.queryByText('This screen is under construction.')).toBeNull();
  });

  it('renders the page in Arabic', async () => {
    await openStats('ar');

    expect(await screen.findByRole('heading', { level: 1, name: 'إحصائياتي' })).toBeInTheDocument();
    expect(
      await (await region('مراجعاتي وردودي')).findByText(/^الالتزام بمهلة الرد: 92\.5\u200E?%\u200E?$/u),
    ).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });
});
