import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { describe, expect, it, vi } from 'vitest';
import {
  getGetInfrastructureConfigurationMockHandler,
  getGetRuntimeSettingsMockHandler,
  getResetRuntimeSettingMockHandler,
  getUpdateRuntimeSettingMockHandler,
} from '@/shared/api/generated/configuration/configuration.msw';
import type { InfrastructureConfigurationResult, RuntimeSettingGroupResult } from '@/shared/api/generated/model';
import { axe } from '@/test/axe';
import {
  choiceListSetting,
  choiceSetting,
  featureFlag,
  infrastructure,
  reachableAi,
  runtimeSetting,
  settingGroups,
} from '@/test/configurationFixtures';
import { server } from '@/test/msw/server';
import { renderApp } from '@/test/renderWithProviders';
import { testSessions } from '@/test/sessions';

const slaLabel = 'Teacher reply time (hours)';

const openPage = (
  groups: RuntimeSettingGroupResult[] = settingGroups(runtimeSetting()),
  infra: InfrastructureConfigurationResult = infrastructure(),
  lng: 'en' | 'ar' = 'en',
) => {
  server.use(getGetRuntimeSettingsMockHandler(groups), getGetInfrastructureConfigurationMockHandler(infra));
  return renderApp('/admin/configuration', { session: testSessions.admin, lng });
};

const rowOf = async (label: string) => {
  const text = await screen.findByText(label);
  const row = text.closest('li');
  if (!row) {
    throw new Error('No row for ' + label);
  }
  return within(row);
};

const captureBodies = () => {
  const bodies: unknown[] = [];
  server.use(
    getUpdateRuntimeSettingMockHandler(async ({ request }) => {
      bodies.push(await request.json());
      return runtimeSetting();
    }),
  );
  return bodies;
};

describe('ConfigurationPage', () => {
  it('shows settings grouped by section after loading', async () => {
    openPage();

    expect(await screen.findByRole('status', { name: 'Loading settings…' })).toBeInTheDocument();
    expect(await screen.findByRole('heading', { level: 2, name: 'Ask a Teacher' })).toBeInTheDocument();
    const row = await rowOf(slaLabel);
    expect(row.getByText('24')).toBeInTheDocument();
    expect(row.getAllByText('Default').length).toBeGreaterThan(0);
  });

  it('shows the empty state when there are no settings', async () => {
    openPage([]);

    expect(await screen.findByText('No editable settings.')).toBeInTheDocument();
  });

  it('shows retry on error and recovers', async () => {
    const user = userEvent.setup();
    openPage();
    server.use(
      http.get('*/api/configuration/settings', () =>
        HttpResponse.json({ code: 'UNHANDLED_EXCEPTION' }, { status: 500 }),
      ),
    );

    expect(await screen.findByText('Could not load the settings.')).toBeInTheDocument();
    server.use(getGetRuntimeSettingsMockHandler(settingGroups(runtimeSetting())));
    await user.click(screen.getByRole('button', { name: 'Retry' }));

    expect(await screen.findByText(slaLabel)).toBeInTheDocument();
  });

  it('saves a new integer value and shows the changed badge', async () => {
    const user = userEvent.setup();
    openPage();
    const bodies = captureBodies();
    const row = await rowOf(slaLabel);
    server.use(getGetRuntimeSettingsMockHandler(settingGroups(runtimeSetting({ value: 30, isOverridden: true }))));

    await user.type(row.getByLabelText('New value'), '30');
    await user.click(row.getByRole('button', { name: 'Save' }));

    expect(await screen.findByText('Setting saved.')).toBeInTheDocument();
    expect(bodies).toEqual([{ value: 30 }]);
    expect(await screen.findByText('Changed')).toBeInTheDocument();
    expect(screen.getByText('Default: 24')).toBeInTheDocument();
  });

  it('shows an inline range error and sends nothing', async () => {
    const user = userEvent.setup();
    openPage();
    const bodies = captureBodies();
    const row = await rowOf(slaLabel);

    await user.type(row.getByLabelText('New value'), '999');
    await user.click(row.getByRole('button', { name: 'Save' }));

    expect(await row.findByText('Enter a value between 1 and 168.')).toBeInTheDocument();
    expect(row.getByLabelText('New value')).toHaveFocus();
    expect(bodies).toEqual([]);
  });

  it('shows the server error inline when the change is refused', async () => {
    const user = userEvent.setup();
    openPage();
    server.use(
      http.put('*/api/configuration/settings/:key', () =>
        HttpResponse.json({ code: 'ASK_TEACHER_REMINDER_ORDER_INVALID' }, { status: 400 }),
      ),
    );
    const row = await rowOf(slaLabel);

    await user.type(row.getByLabelText('New value'), '30');
    await user.click(row.getByRole('button', { name: 'Save' }));

    expect(
      await row.findByText(
        'The first reminder must come before the second, and the second before the end of the reply time.',
      ),
    ).toBeInTheDocument();
  });

  it('resets a changed setting to its default', async () => {
    const user = userEvent.setup();
    const keys: unknown[] = [];
    openPage(settingGroups(runtimeSetting({ value: 30, isOverridden: true })));
    server.use(
      getResetRuntimeSettingMockHandler(({ params }) => {
        keys.push(params.key);
        return runtimeSetting();
      }),
    );
    const row = await rowOf(slaLabel);

    await user.click(row.getByRole('button', { name: 'Reset to default' }));

    expect(await screen.findByText('Default restored.')).toBeInTheDocument();
    expect(keys).toEqual(['askTeacher.replySlaHours']);
  });

  it('hides reset when the setting uses its default', async () => {
    openPage();

    await rowOf(slaLabel);
    expect(screen.queryByRole('button', { name: 'Reset to default' })).not.toBeInTheDocument();
  });

  it('turns a feature flag on', async () => {
    const user = userEvent.setup();
    openPage(settingGroups(featureFlag()));
    const bodies = captureBodies();
    const row = await rowOf('Require opening every lesson before a unit exam');

    await user.click(row.getByRole('checkbox', { name: 'Enabled' }));
    await user.click(row.getByRole('button', { name: 'Save' }));

    await vi.waitFor(() => {
      expect(bodies).toEqual([{ value: true }]);
    });
  });

  it('saves choice and choice-list settings', async () => {
    const user = userEvent.setup();
    openPage(settingGroups(choiceSetting(), choiceListSetting()));
    const bodies = captureBodies();
    const choice = await rowOf('Reminder channel');
    const list = await rowOf('Reminder channels');

    await user.selectOptions(choice.getByLabelText('New value'), 'Email');
    await user.click(choice.getByRole('button', { name: 'Save' }));
    await user.click(list.getByRole('checkbox', { name: 'WhatsApp' }));
    await user.click(list.getByRole('checkbox', { name: 'Email' }));
    await user.click(list.getByRole('button', { name: 'Save' }));

    await vi.waitFor(() => {
      expect(bodies).toEqual([{ value: 'Email' }, { value: ['WhatsApp', 'Email'] }]);
    });
  });

  it('labels the out-of-app reminder choices and saves a new stage', async () => {
    const user = userEvent.setup();
    openPage(
      settingGroups(
        choiceSetting({
          key: 'askTeacher.outOfAppReminderChannels',
          group: 'AskTeacher',
          labelEnglish: 'Out-of-app reminder channels',
          allowedValues: ['WhatsApp', 'Email', 'Both'],
          value: 'Both',
          defaultValue: 'Both',
        }),
        choiceSetting({
          key: 'askTeacher.outOfAppReminderStage',
          group: 'AskTeacher',
          labelEnglish: 'Out-of-app reminder stage',
          allowedValues: ['FirstReminder', 'SecondReminder'],
          value: 'SecondReminder',
          defaultValue: 'SecondReminder',
        }),
      ),
    );
    const bodies = captureBodies();
    const channels = await rowOf('Out-of-app reminder channels');
    const stage = await rowOf('Out-of-app reminder stage');

    expect(channels.getByRole('option', { name: 'Both' })).toBeInTheDocument();
    await user.selectOptions(stage.getByLabelText('New value'), 'First reminder');
    await user.click(stage.getByRole('button', { name: 'Save' }));

    await vi.waitFor(() => {
      expect(bodies).toEqual([{ value: 'FirstReminder' }]);
    });
  });

  it('lists the teacher reminder providers read-only', async () => {
    openPage();

    expect(await screen.findByRole('row', { name: /Teacher reminder by WhatsApp/ })).toHaveTextContent('Fake');
    expect(screen.getByRole('row', { name: /Teacher reminder by email/ })).toBeInTheDocument();
  });

  it('shows providers, the safety switch and secret status read-only', async () => {
    openPage();

    const payments = await screen.findByRole('row', { name: /Payments/ });
    expect(payments).toHaveTextContent('Fake');
    expect(screen.getByRole('row', { name: /Sign-in code by SMS/ })).toHaveTextContent('Disabled');
    const safety = screen.getByText('Allow fake payments').closest('li');
    expect(safety).toHaveTextContent('Off');
    expect(screen.queryByRole('checkbox', { name: /Allow fake payments/ })).not.toBeInTheDocument();
    expect(screen.getByText('Payments:Paymob:SecretKey').closest('li')).toHaveTextContent('Not set');
    expect(screen.getByText('CoreJwt:Key').closest('li')).toHaveTextContent('Set');
    expect(screen.getByText('The API uses its built-in fakes and does not call the AI service.')).toBeInTheDocument();
  });

  it('shows the AI service models when reachable and an alert when unreachable', async () => {
    const first = openPage(settingGroups(runtimeSetting()), reachableAi());

    expect(await screen.findAllByText('gpt-5.6-luna')).not.toHaveLength(0);
    expect(screen.getByText('ELMANHG_AI_OPENAI_API_KEY').closest('li')).toHaveTextContent('Set');
    first.unmount();

    openPage(settingGroups(runtimeSetting()), infrastructure({ aiServiceStatus: 'Unreachable' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('The AI service could not be reached.');
  });

  it('renders right to left in Arabic', async () => {
    openPage(settingGroups(runtimeSetting()), infrastructure(), 'ar');

    expect(await screen.findByRole('heading', { level: 1, name: 'الإعدادات' })).toBeInTheDocument();
    expect(await screen.findByText('مهلة رد المعلّم (ساعات)')).toBeInTheDocument();
    expect(document.documentElement.dir).toBe('rtl');
  });

  it('has no axe violations', async () => {
    const { container } = openPage(settingGroups(runtimeSetting(), featureFlag()));

    await screen.findByText(slaLabel);
    await screen.findByRole('row', { name: /Payments/ });
    expect((await axe(container)).violations).toEqual([]);
  });
});
