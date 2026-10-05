import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it } from 'vitest';
import {
  getRegisterWithEmailMockHandler,
  getSendOtpMockHandler,
  getVerifyOtpMockHandler,
} from '@/shared/api/generated/auth/auth.msw';
import type {
  AuthResult,
  RecordFunnelEventRequest,
  RegisterWithEmailCommand,
  RegisterWithPhoneCommand,
} from '@/shared/api/generated/model';
import { getGetSubjectInterestsMockHandler } from '@/shared/api/generated/students/students.msw';
import { termsVersion } from '@/shared/lib/terms';
import { axe } from '@/test/axe';
import { server } from '@/test/msw/server';
import { subjectInterests } from '@/test/onboardingFixtures';
import { renderApp } from '@/test/renderWithProviders';

const studentResult: AuthResult = {
  accessToken: 'token-student',
  user: {
    id: 's1',
    displayName: 'Mona',
    role: 'Student',
    phoneNumber: null,
    email: 'mona@elmanhg.test',
    needsOnboarding: true,
  },
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
  it('creates a student account with email and lands on onboarding', async () => {
    server.use(getRegisterWithEmailMockHandler(studentResult), getGetSubjectInterestsMockHandler(subjectInterests()));
    const user = userEvent.setup();
    renderApp('/signup');

    await fillEmailSignUp(user);

    expect(await screen.findByRole('heading', { name: 'Choose your subjects' })).toBeInTheDocument();
  });

  it('records sign-up started and completed events', async () => {
    const types: string[] = [];
    server.use(
      getRegisterWithEmailMockHandler(studentResult),
      getGetSubjectInterestsMockHandler(subjectInterests()),
      http.post('*/api/analytics/funnel-events', async ({ request }) => {
        types.push(((await request.json()) as RecordFunnelEventRequest).type);
        return new HttpResponse(null, { status: 200 });
      }),
    );
    const user = userEvent.setup();
    renderApp('/signup');

    await fillEmailSignUp(user);

    expect(await screen.findByRole('heading', { name: 'Choose your subjects' })).toBeInTheDocument();
    expect(types).toEqual(['SignUpStarted', 'SignUpCompleted']);
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
      getGetSubjectInterestsMockHandler(subjectInterests()),
    );
    const user = userEvent.setup();
    renderApp('/signup');

    await signUpWithPhone(user);

    expect(await screen.findByRole('heading', { name: 'Choose your subjects' })).toBeInTheDocument();
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

  it('shows the app bar with the logo linking home', async () => {
    renderApp('/signup');

    const banner = await screen.findByRole('banner');

    expect(within(banner).getByRole('link', { name: 'Elmanhg' })).toHaveAttribute('href', '/');
  });

  it('shows the terms line with a link to the privacy page', async () => {
    renderApp('/signup');

    expect(await screen.findByText(/By signing up, you agree to the terms and privacy policy/u)).toBeInTheDocument();
    const link = screen.getByRole('link', { name: /Read the terms and privacy policy/u });
    expect(link).toHaveAttribute('href', '/privacy');
    expect(link).toHaveAttribute('target', '_blank');
  });

  it('keeps the terms line under the code step of a mobile sign-up', async () => {
    server.use(getSendOtpMockHandler());
    const user = userEvent.setup();
    renderApp('/signup');

    await user.type(await screen.findByLabelText('Your name'), 'Ahmed');
    await user.type(screen.getByLabelText('Mobile number'), '01012345678');
    await user.click(screen.getByRole('button', { name: 'Send code' }));

    expect(await screen.findByLabelText('Verification code')).toBeInTheDocument();
    expect(screen.getByText(/By signing up, you agree to the terms and privacy policy/u)).toBeInTheDocument();
  });

  it('shows the terms line in Arabic', async () => {
    renderApp('/signup', { lng: 'ar' });

    expect(await screen.findByText(/لو سنك أقل من 18 سنة، لازم ولي أمرك يكون موافق\./u)).toBeInTheDocument();
  });

  it('sends the displayed terms version with an email sign-up', async () => {
    let body: RegisterWithEmailCommand | undefined;
    server.use(
      http.post('*/api/auth/register/email', async ({ request }) => {
        body = (await request.json()) as RegisterWithEmailCommand;
        return HttpResponse.json(studentResult);
      }),
      getGetSubjectInterestsMockHandler(subjectInterests()),
    );
    const user = userEvent.setup();
    renderApp('/signup');

    await fillEmailSignUp(user);

    expect(await screen.findByRole('heading', { name: 'Choose your subjects' })).toBeInTheDocument();
    expect(body?.termsVersion).toBe(termsVersion);
  });

  it('sends the displayed terms version with a mobile sign-up', async () => {
    let body: RegisterWithPhoneCommand | undefined;
    server.use(
      getSendOtpMockHandler(),
      getVerifyOtpMockHandler({}),
      http.post('*/api/auth/register/phone', async ({ request }) => {
        body = (await request.json()) as RegisterWithPhoneCommand;
        return HttpResponse.json(studentResult);
      }),
      getGetSubjectInterestsMockHandler(subjectInterests()),
    );
    const user = userEvent.setup();
    renderApp('/signup');

    await signUpWithPhone(user);

    expect(await screen.findByRole('heading', { name: 'Choose your subjects' })).toBeInTheDocument();
    expect(body?.termsVersion).toBe(termsVersion);
  });

  it('asks to reload when the terms version is out of date', async () => {
    server.use(http.post('*/api/auth/register/email', () => apiError(422, 'TERMS_VERSION_UNKNOWN')));
    const user = userEvent.setup();
    renderApp('/signup');

    await fillEmailSignUp(user);

    expect(await screen.findByRole('alert')).toHaveTextContent('This page is out of date. Reload it and try again.');
  });
});
