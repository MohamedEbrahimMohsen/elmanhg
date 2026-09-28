import { useTranslation } from 'react-i18next';
import { useGetSubject } from '@/shared/api/generated/subjects/subjects';
import { useUnitMutations } from '../hooks/useUnitMutations';
import { ContentEmptyState } from './ContentEmptyState';
import { ContentErrorState } from './ContentErrorState';
import { ContentListSkeleton } from './ContentListSkeleton';
import { NameForm } from './NameForm';
import { UnitItem } from './UnitItem';

export interface UnitPanelProps {
  subjectId: string;
  id: string;
}

const unitNameErrorFields = { UNIT_NAME_REQUIRED: 'name', UNIT_NAME_TOO_LONG: 'name' } as const;

export function UnitPanel({ subjectId, id }: UnitPanelProps) {
  const { t } = useTranslation('content');
  const { data, error, isPending, isError, refetch } = useGetSubject(subjectId);
  const { create } = useUnitMutations(subjectId);

  const renderUnits = () => {
    if (isPending) {
      return <ContentListSkeleton label={t('units.loading')} />;
    }
    if (isError) {
      return (
        <ContentErrorState
          title={t('units.errorTitle')}
          error={error}
          onRetry={() => {
            void refetch();
          }}
        />
      );
    }
    if (data.units.length === 0) {
      return <ContentEmptyState message={t('units.empty')} />;
    }
    return (
      <ol aria-label={t('units.listLabel', { name: data.name })} className="flex flex-col gap-2">
        {data.units.map((unit, index) => (
          <UnitItem
            key={unit.id}
            subjectId={subjectId}
            unit={unit}
            isFirst={index === 0}
            isLast={index === data.units.length - 1}
            position={index + 1}
          />
        ))}
      </ol>
    );
  };

  return (
    <div id={id} className="flex flex-col gap-3 border-t border-border pt-3">
      {renderUnits()}
      <NameForm
        label={t('units.nameLabel')}
        submitLabel={t('units.add')}
        serverErrorFields={unitNameErrorFields}
        onSubmit={create}
      />
    </div>
  );
}
