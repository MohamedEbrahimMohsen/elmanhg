import { useTranslation } from 'react-i18next';
import { useGetSubjects } from '@/shared/api/generated/subjects/subjects';
import { useTeacherSubjects } from '../hooks/useTeacherSubjects';

export interface TeacherSubjectsCellProps {
  teacherId: string;
  teacherName: string;
  subjectIds: string[];
}

export function TeacherSubjectsCell({ teacherId, teacherName, subjectIds }: TeacherSubjectsCellProps) {
  const { t } = useTranslation('users');
  const subjects = useGetSubjects();
  const { toggle, isPending } = useTeacherSubjects();

  if (!subjects.data) {
    return <p className="text-caption text-text-muted">{t('subjects.loading')}</p>;
  }

  return (
    <fieldset className="flex flex-wrap gap-x-4 gap-y-2">
      <legend className="sr-only">{t('subjects.label', { name: teacherName })}</legend>
      {subjects.data.map((subject) => (
        <label key={subject.id} className="inline-flex min-h-6 items-center gap-2 text-caption text-text">
          <input
            type="checkbox"
            checked={subjectIds.includes(subject.id)}
            disabled={isPending}
            onChange={(event) => {
              toggle(teacherId, subject.id, event.target.checked);
            }}
            className="size-4 accent-accent focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
          />
          {subject.name}
        </label>
      ))}
    </fieldset>
  );
}
