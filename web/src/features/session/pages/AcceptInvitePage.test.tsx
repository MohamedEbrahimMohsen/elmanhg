import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import {
  getAcceptInvitationMockHandler,
  getSendOtpMockHandler,
  getSendOtpResponseMock,
  getVerifyOtpMockHandler,
} from '@/shared/api/generated/auth/auth.msw';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const verificationId = '9a9a9a9a-9a9a-49a9-89a9-9a9a9a9a9a9a';

type User = ReturnType<typeof userEvent.setup>;

function serveCode() {
  server.use(
    getSendOtpMockHandler(getSendOtpResponseMock({ verificationId, channel: 'Email' })),
    getVerifyOtpMockHandler({}),
  );
}

async function reachPasswordStep(user: User) {
  await user.type(await screen.findByLabelText('Email'), 'omar@example.test');
  await user.click(screen.getByRole('button', { name: 'Send code' }));
  await user.type(await screen.findByLabelText('Verification code'), '123456');
  await user.click(screen.getByRole('button', { name: 'Verify' }));
}

async function setPassword(user: User, confirm = 'Invited2026') {
  await user.type(await screen.findByLabelText('New password'), 'Invited2026');
  await user.type(screen.getByLabelText('Confirm password'), confirm);
  await user.click(screen.getByRole('button', { name: 'Save password and sign in' }));
}

describe('AcceptInvitePage', () => {
  it('email → code → password signs the teacher in and lands on /teacher', async () => {
    const bodies: unknown[] = [];
    serveCode();
    server.use(
      getAcceptInvitationMockHandler(async ({ request }) => {
        bodies.push(await request.json());
        return {
          accessToken: 'token-teacher',
          user: {
            id: 't9',
            displayName: 'Omar',
            role: 'Teacher',
            phoneNumber: null,
            email: 'omar@example.test',
            needsOnboarding: false,
          },
        };
      }),
    );
    const user = userEvent.setup();
    const { router } = renderApp('/accept-invite');

    await reachPasswordStep(user);
    await setPassword(user);

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/teacher');
    });
    expect(bodies).toEqual([{ verificationId, password: 'Invited2026' }]);
  });

  it('shows INVITATION_NOT_FOUND as a form error', async () => {
    serveCode();
    server.use(
      http.post('*/api/auth/invitations/accept', () =>
        HttpResponse.json({ code: 'INVITATION_NOT_FOUND', message: '' }, { status: 404 }),
      ),
    );
    const user = userEvent.setup();
    renderApp('/accept-invite');

    await reachPasswordStep(user);
    await setPassword(user);

    expect(await screen.findByRole('alert')).toHaveTextContent('There is no pending invitation for this email.');
  });

  it('shows a mismatch inline', async () => {
    serveCode();
    const user = userEvent.setup();
    renderApp('/accept-invite');

    await reachPasswordStep(user);
    await setPassword(user, 'Invited2027');

    expect(await screen.findByText('Passwords do not match.')).toBeInTheDocument();
    expect(screen.getByLabelText('Confirm password')).toHaveAttribute('aria-invalid', 'true');
  });

  it('redirects a signed-in visitor to their home', async () => {
    const { router } = renderApp('/accept-invite', { session: testSessions.teacher });

    await waitFor(() => {
      expect(router.state.location.pathname).toBe('/teacher');
    });
  });

  it('has no axe violations', async () => {
    const { container } = renderApp('/accept-invite');

    await screen.findByRole('heading', { level: 1, name: 'Accept your invitation' });

    expect((await axe(container)).violations).toEqual([]);
  });
});
