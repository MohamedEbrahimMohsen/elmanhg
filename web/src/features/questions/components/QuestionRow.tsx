import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import type { QuestionListItemResult } from '@/shared/api/generated/model';
import { formatNumber } from '@/shared/lib/format';
import { Button } from '@/shared/ui/button';
import { stemExcerpt } from '../api/stemExcerpt';
import { QuestionStatusBadge } from './QuestionStatusBadge';

export interface QuestionRowProps {
  item: QuestionListItemResult;
}

const cellClassName = 'px-2.5 py-2.25 align-top text-caption';

export function QuestionRow({ item }: QuestionRowProps) {
  const { t, i18n } = useTranslation('questions');
  const lng = i18n.resolvedLanguage ?? i18n.language;

  return (
    <tr className="border-t border-border hover:bg-soft">
      <td className={cellClassName}>{stemExcerpt(item.stem)}</td>
      <td className={cellClassName}>{item.lessonName}</td>
      <td className={cellClassName}>{t([`types.${item.type}`, item.type])}</td>
      <td className={cellClassName}>
        <QuestionStatusBadge status={item.validationStatus} />
      </td>
      <td className={cellClassName}>
        {t('list.table.versionValue', { version: formatNumber(Number(item.version), lng, 'latin') })}
      </td>
      <td className={cellClassName}>{item.teacherName ?? t('list.table.noTeacher')}</td>
      <td className={cellClassName}>{item.rejectionReason ?? ''}</td>
      <td className={cellClassName}>
        <Button asChild size="sm" variant="secondary">
          <Link to="/admin/question/$questionId" params={{ questionId: item.id }}>
            {item.validationStatus === 'Rejected' ? t('list.table.editAndResubmit') : t('list.table.edit')}
          </Link>
        </Button>
      </td>
    </tr>
  );
}
