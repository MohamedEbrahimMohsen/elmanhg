import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import {
  getLoginWithEmailMockHandler,
  getLoginWithPhoneMockHandler,
  getSendOtpMockHandler,
  getSendOtpResponseMock,
  getVerifyOtpMockHandler,
} from '@/shared/api/generated/auth/auth.msw';
import { getMasteryMock } from '@/shared/api/generated/mastery/mastery.msw';
import type { AuthResult } from '@/shared/api/generated/model';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const phoneNumber = '01012345678';

const authResult = (role: string): AuthResult => ({
  accessToken: `token-${role}`,
  user: { id: 'u1', displayName: 'Mona', role, phoneNumber: null, email: null },
});

const apiError = (status: number, code: string) => HttpResponse.json({ code, message: '' }, { status });

type User = ReturnType<typeof userEvent.setup>;

async function signInWithEmail(user: User) {
  await user.click(await screen.findByRole('button', { name: 'Email' }));
  await user.type(screen.getByLabelText('Email'), 'admin@elmanhg.test');
  await user.type(screen.getByLabelText('Password'), 'Password1');
  await user.click(screen.getByRole('button', { name: 'Continue' }));
}

async function requestCode(user: User) {
  await user.type(await screen.findByLabelText('Mobile number'), phoneNumber);
  await user.click(screen.getByRole('button', { name: 'Send code' }));
}

async function submitCode(user: User) {
  await user.type(await screen.findByLabelText('Verification code'), '123456');
  await user.click(screen.getByRole('button', { name: 'Verify' }));
}

describe('LoginPage', () => {
  it('signs an admin in with email and lands on the admin home', async () => {
    server.use(getLoginWithEmailMockHandler(authResult('Admin')));
    const user = userEvent.setup();
    renderApp('/login');

    await signInWithEmail(user);

    expect(await screen.findByRole('heading', { name: 'Dashboard' })).toBeInTheDocument();
  });

  it('returns to the originally requested page after signing in', async () => {
    server.use(getLoginWithEmailMockHandler(authResult('Admin')));
    const user = userEvent.setup();
    renderApp('/admin/users');

    await signInWithEmail(user);

    expect(await screen.findByRole('heading', { name: 'Users' })).toBeInTheDocument();
  });

  it('shows the invalid-login message on wrong credentials', async () => {
    server.use(http.post('*/api/auth/login/email', () => apiError(400, 'USER_INVALID_LOGIN')));
    const user = userEvent.setup();
    renderApp('/login');

    await signInWithEmail(user);

    expect(await screen.findByRole('alert')).toHaveTextContent('Email or password is incorrect.');
  });

  it('signs a student in with a mobile code', async () => {
    server.use(
      getSendOtpMockHandler(),
      getVerifyOtpMockHandler({}),
      getLoginWithPhoneMockHandler(authResult('Student')),
      ...getMasteryMock(),
    );
    const user = userEvent.setup();
    renderApp('/login');

    await requestCode(user);
    expect(await screen.findByText('We sent a 6-digit code to 01012345678.')).toBeInTheDocument();
    await submitCode(user);

    expect(await screen.findByRole('heading', { name: 'Hello, Mona' })).toBeInTheDocument();
  });

  it('offers sign-up when the mobile number has no account', async () => {
    server.use(
      getSendOtpMockHandler(),
      getVerifyOtpMockHandler({}),
      http.post('*/api/auth/login/phone', () => apiError(404, 'PHONE_NUMBER_NOT_REGISTERED')),
    );
    const user = userEvent.setup();
    renderApp('/login');

    await requestCode(user);
    await submitCode(user);

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'No account uses this mobile number. Create an account first.',
    );
    expect(screen.getByRole('link', { name: 'Create an account' })).toBeInTheDocument();
  });

  it('shows the wrong-code error on the code field', async () => {
    server.use(
      getSendOtpMockHandler(),
      http.post('*/api/auth/otp/verify', () => apiError(400, 'OTP_NOT_MATCHED')),
    );
    const user = userEvent.setup();
    renderApp('/login');

    await requestCode(user);
    await submitCode(user);

    expect(await screen.findByText('The code is incorrect.')).toBeInTheDocument();
    expect(screen.getByLabelText('Verification code')).toHaveAttribute('aria-invalid', 'true');
  });

  it('sends a new code and uses its verification id', async () => {
    const verificationIds = ['first-id', 'second-id'];
    server.use(
      getSendOtpMockHandler(() => getSendOtpResponseMock({ verificationId: verificationIds.shift() ?? 'unexpected' })),
      http.post('*/api/auth/otp/verify', async ({ request }) => {
        const body = (await request.json()) as { verificationId: string };
        return body.verificationId === 'second-id' ? HttpResponse.json({}) : apiError(400, 'OTP_INVALID');
      }),
      getLoginWithPhoneMockHandler(authResult('Student')),
      ...getMasteryMock(),
    );
    const user = userEvent.setup();
    renderApp('/login');

    await requestCode(user);
    await user.click(await screen.findByRole('button', { name: 'Resend code' }));
    expect(await screen.findByText('A new code is on its way.')).toBeInTheDocument();
    await submitCode(user);

    expect(await screen.findByRole('heading', { name: 'Hello, Mona' })).toBeInTheDocument();
  });

  it('does not verify twice when sign-in fails after verification', async () => {
    let verifyCalls = 0;
    let loginCalls = 0;
    server.use(
      getSendOtpMockHandler(),
      http.post('*/api/auth/otp/verify', () => {
        verifyCalls += 1;
        return verifyCalls === 1 ? HttpResponse.json({}) : apiError(400, 'OTP_ALREADY_VERIFIED');
      }),
      http.post('*/api/auth/login/phone', () => {
        loginCalls += 1;
        return loginCalls === 1 ? apiError(500, 'UNHANDLED_EXCEPTION') : HttpResponse.json(authResult('Student'));
      }),
      ...getMasteryMock(),
    );
    const user = userEvent.setup();
    renderApp('/login');

    await requestCode(user);
    await submitCode(user);
    expect(await screen.findByRole('alert')).toHaveTextContent('Something went wrong. Please try again.');
    await user.click(screen.getByRole('button', { name: 'Verify' }));

    expect(await screen.findByRole('heading', { name: 'Hello, Mona' })).toBeInTheDocument();
  });

  it('sends a signed-in visitor away from sign in to their home', async () => {
    server.use(...getMasteryMock());
    renderApp('/login', { session: testSessions.student });

    expect(await screen.findByRole('heading', { name: 'Hello, أحمد' })).toBeInTheDocument();
  });

  it('renders right-to-left in Arabic', async () => {
    renderApp('/login', { lng: 'ar' });

    expect(await screen.findByRole('heading', { name: 'تسجيل الدخول' })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute('dir', 'rtl');
  });

  it('has no axe violations', async () => {
    const { container } = renderApp('/login');

    await screen.findByRole('heading', { name: 'Sign in' });

    expect((await axe(container)).violations).toEqual([]);
  });
});
