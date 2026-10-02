import type {
  AiServiceConfigurationResult,
  ExamPeriodResult,
  InfrastructureConfigurationResult,
  RuntimeSettingGroup,
  RuntimeSettingGroupResult,
  RuntimeSettingResult,
} from '@/shared/api/generated/model';

export function runtimeSetting(overrides: Partial<RuntimeSettingResult> = {}): RuntimeSettingResult {
  return {
    key: 'askTeacher.replySlaHours',
    group: 'AskTeacher',
    type: 'Integer',
    value: 24,
    defaultValue: 24,
    isOverridden: false,
    minimum: 1,
    maximum: 168,
    allowedValues: [],
    labelArabic: 'مهلة رد المعلّم (ساعات)',
    labelEnglish: 'Teacher reply time (hours)',
    descriptionArabic: 'المدة التي يجب أن يرد فيها المعلّم على سؤال الطالب.',
    descriptionEnglish: 'How long a teacher has to reply to a student question.',
    updatedAt: null,
    ...overrides,
  };
}

export function settingGroups(...settings: RuntimeSettingResult[]): RuntimeSettingGroupResult[] {
  const groups = new Map<RuntimeSettingGroup, RuntimeSettingResult[]>();
  for (const setting of settings) {
    groups.set(setting.group, [...(groups.get(setting.group) ?? []), setting]);
  }
  return [...groups].map(([group, groupSettings]) => ({ group, settings: groupSettings }));
}

export function featureFlag(overrides: Partial<RuntimeSettingResult> = {}): RuntimeSettingResult {
  return runtimeSetting({
    key: 'features.examsRequireAllLessonsOpened',
    group: 'Features',
    type: 'Boolean',
    value: false,
    defaultValue: false,
    minimum: null,
    maximum: null,
    labelArabic: 'اشتراط فتح كل دروس الوحدة قبل امتحانها',
    labelEnglish: 'Require opening every lesson before a unit exam',
    ...overrides,
  });
}

export function choiceSetting(overrides: Partial<RuntimeSettingResult> = {}): RuntimeSettingResult {
  return runtimeSetting({
    key: 'test.choice',
    group: 'Features',
    type: 'Choice',
    value: 'WhatsApp',
    defaultValue: 'WhatsApp',
    minimum: null,
    maximum: null,
    allowedValues: ['WhatsApp', 'Email'],
    labelArabic: 'قناة التذكير',
    labelEnglish: 'Reminder channel',
    ...overrides,
  });
}

export function choiceListSetting(overrides: Partial<RuntimeSettingResult> = {}): RuntimeSettingResult {
  return choiceSetting({
    key: 'test.choiceList',
    type: 'ChoiceList',
    value: [],
    defaultValue: [],
    labelArabic: 'قنوات التذكير',
    labelEnglish: 'Reminder channels',
    ...overrides,
  });
}

export function infrastructure(
  overrides: Partial<InfrastructureConfigurationResult> = {},
): InfrastructureConfigurationResult {
  return {
    environment: 'Testing',
    integrations: [
      { integration: 'otpWhatsApp', provider: 'Fake', mode: 'Fake', isEnabled: true },
      { integration: 'otpEmail', provider: 'Fake', mode: 'Fake', isEnabled: true },
      { integration: 'otpSms', provider: 'Http', mode: 'Fake', isEnabled: false },
      { integration: 'invitationEmail', provider: 'Fake', mode: 'Fake', isEnabled: true },
      { integration: 'teacherReminderWhatsApp', provider: 'Fake', mode: 'Fake', isEnabled: true },
      { integration: 'teacherReminderEmail', provider: 'Fake', mode: 'Fake', isEnabled: true },
      { integration: 'payments', provider: 'Fake', mode: 'Fake', isEnabled: true },
      { integration: 'fileStorage', provider: 'Local', mode: 'Local', isEnabled: true },
      { integration: 'aiService', provider: 'Fake', mode: 'Fake', isEnabled: true },
    ],
    safetySwitches: [{ key: 'Payments:AllowFakePayments', isOn: false }],
    secrets: [
      { key: 'Payments:Paymob:SecretKey', isSet: false },
      { key: 'CoreJwt:Key', isSet: true },
    ],
    aiServiceStatus: 'NotUsed',
    aiService: null,
    ...overrides,
  };
}

export function reachableAi(): InfrastructureConfigurationResult {
  const aiService: AiServiceConfigurationResult = {
    llmProvider: 'openai_compatible',
    chatModel: 'gpt-5.6-luna',
    essayGradingModel: 'gpt-5.6-luna',
    mathStepGradingModel: 'gpt-5.6-luna',
    embeddingProvider: 'openai',
    embeddingModel: 'text-embedding-3-small',
    transcriptionProvider: 'openai',
    transcriptionModel: 'whisper-1',
    secrets: [{ key: 'ELMANHG_AI_OPENAI_API_KEY', isSet: true }],
  };
  return infrastructure({ aiServiceStatus: 'Reachable', aiService });
}

export function slaCalendarSetting(overrides: Partial<RuntimeSettingResult> = {}): RuntimeSettingResult {
  return runtimeSetting({
    key: 'slaCalendar.skipWeekends',
    group: 'SlaCalendar',
    type: 'Boolean',
    value: true,
    defaultValue: true,
    minimum: null,
    maximum: null,
    labelArabic: 'استبعاد أيام العطلة',
    labelEnglish: 'Skip weekends',
    descriptionArabic: 'عند التفعيل لا تحتسب أيام العطلة خارج فترات الامتحانات من مهلة الرد والتذكيرات.',
    descriptionEnglish:
      'When on, weekend days outside exam periods do not count toward the reply time and the reminders.',
    ...overrides,
  });
}

export function examPeriod(overrides: Partial<ExamPeriodResult> = {}): ExamPeriodResult {
  return {
    id: 'c7c7c7c7-c7c7-4c7c-8c7c-c7c7c7c7c7c7',
    name: 'Final exams',
    startDate: '2026-06-01',
    endDate: '2026-07-15',
    createdAt: '2026-05-20T09:00:00Z',
    updatedAt: null,
    ...overrides,
  };
}
