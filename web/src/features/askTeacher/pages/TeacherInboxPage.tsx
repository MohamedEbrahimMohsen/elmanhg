import { useTranslation } from 'react-i18next';
import { InboxFilterTabs } from '../components/InboxFilterTabs';
import { InboxList } from '../components/InboxList';
import { InboxReminders } from '../components/InboxReminders';
import { useTeacherInboxSearch } from '../hooks/useTeacherInboxSearch';

export function TeacherInboxPage() {
  const { t } = useTranslation('askTeacher');
  const { filter, setFilter } = useTeacherInboxSearch();

  return (
    <section className="flex flex-col gap-4">
      <h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('inbox.title')}</h1>
      <InboxReminders />
      <InboxFilterTabs filter={filter} onChange={setFilter} />
      <InboxList />
    </section>
  );
}
