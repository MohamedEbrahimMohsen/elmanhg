import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import {
  getRegisterWithEmailMockHandler,
  getSendOtpMockHandler,
  getVerifyOtpMockHandler,
} from '@/shared/api/generated/auth/auth.msw';
import { getMasteryMock } from '@/shared/api/generated/mastery/mastery.msw';
import type { AuthResult } from '@/shared/api/generated/model';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';

const studentResult: AuthResult = {
  accessToken: 'token-student',
  user: { id: 's1', displayName: 'Mona', role: 'Student', phoneNumber: null, email: 'mona@elmanhg.test' },
};

const apiError = (status: number, code: string) => HttpResponse.json({ code, message: '' }, { status });

type User = ReturnType<typeof userEvent.setup>;

async function fillEmailSignUp(user: User) {
  await user.click(await screen.findByRole('button', { name: 'Email' }));
  await user.type(screen.getByLabelText('Your name'), 'Mona');
  await user.type(screen.getByLabelText('Email'), 'mona@elmanhg.test');
  await user.type(screen.getByLabelText('Password'), 'Password1');
  await user.click(screen.getByRole('button', { name: 'Create account' }));
}

async function signUpWithPhone(user: User) {
  await user.type(await screen.findByLabelText('Your name'), 'Ahmed');
  await user.type(screen.getByLabelText('Mobile number'), '01012345678');
  await user.click(screen.getByRole('button', { name: 'Send code' }));
  await user.type(await screen.findByLabelText('Verification code'), '123456');
  await user.click(screen.getByRole('button', { name: 'Verify' }));
}

describe('SignUpPage', () => {
  it('creates a student account with email and lands on the student home', async () => {
    server.use(getRegisterWithEmailMockHandler(studentResult), ...getMasteryMock());
    const user = userEvent.setup();
    renderApp('/signup');

    await fillEmailSignUp(user);

    expect(await screen.findByRole('heading', { name: 'Hello, Mona' })).toBeInTheDocument();
  });

  it('shows the duplicate-email error on the email field', async () => {
    server.use(http.post('*/api/auth/register/email', () => apiError(409, 'EMAIL_ALREADY_REGISTERED')));
    const user = userEvent.setup();
    renderApp('/signup');

    await fillEmailSignUp(user);

    expect(await screen.findByText('An account already uses this email.')).toBeInTheDocument();
    expect(screen.getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true');
  });

  it('shows required errors and focuses the first field when submitted empty', async () => {
    const user = userEvent.setup();
    renderApp('/signup');

    await user.click(await screen.findByRole('button', { name: 'Email' }));
    await user.click(screen.getByRole('button', { name: 'Create account' }));

    expect(await screen.findByText('This field is required.')).toBeInTheDocument();
    expect(screen.getByLabelText('Your name')).toHaveFocus();
  });

  it('creates a student account with a mobile code', async () => {
    let registeredName: string | undefined;
    server.use(
      getSendOtpMockHandler(),
      getVerifyOtpMockHandler({}),
      http.post('*/api/auth/register/phone', async ({ request }) => {
        registeredName = ((await request.json()) as { displayName: string }).displayName;
        return HttpResponse.json(studentResult);
      }),
      ...getMasteryMock(),
    );
    const user = userEvent.setup();
    renderApp('/signup');

    await signUpWithPhone(user);

    expect(await screen.findByRole('heading', { name: 'Hello, Mona' })).toBeInTheDocument();
    expect(registeredName).toBe('Ahmed');
  });

  it('offers sign-in when the mobile number already has an account', async () => {
    server.use(
      getSendOtpMockHandler(),
      getVerifyOtpMockHandler({}),
      http.post('*/api/auth/register/phone', () => apiError(409, 'PHONE_NUMBER_ALREADY_REGISTERED')),
    );
    const user = userEvent.setup();
    renderApp('/signup');

    await signUpWithPhone(user);

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'An account already uses this mobile number. Sign in instead.',
    );
    expect(screen.getByRole('link', { name: 'Sign in' })).toBeInTheDocument();
  });

  it('has no axe violations', async () => {
    const { container } = renderApp('/signup');

    await screen.findByRole('heading', { name: 'Create account' });

    expect((await axe(container)).violations).toEqual([]);
  });
});
