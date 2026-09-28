import { useTranslation } from 'react-i18next';
import { useGetSubject } from '@/shared/api/generated/subjects/subjects';
import { targetPosition } from '../api/position';
import { useUnitMutations } from '../hooks/useUnitMutations';
import { ContentEmptyState } from './ContentEmptyState';
import { ContentErrorState } from './ContentErrorState';
import { ContentListSkeleton } from './ContentListSkeleton';
import { ItemActions } from './ItemActions';
import { NameForm } from './NameForm';

export interface UnitPanelProps {
  subjectId: string;
  id: string;
}

const unitNameErrorFields = { UNIT_NAME_REQUIRED: 'name', UNIT_NAME_TOO_LONG: 'name' } as const;

export function UnitPanel({ subjectId, id }: UnitPanelProps) {
  const { t } = useTranslation('content');
  const { data, error, isPending, isError, refetch } = useGetSubject(subjectId);
  const { create, rename, move, remove } = useUnitMutations(subjectId);

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
          <li key={unit.id} className="flex flex-col gap-2 rounded-md border border-border bg-bg p-3">
            <span className="text-ui font-semibold text-text">{unit.name}</span>
            <ItemActions
              name={unit.name}
              isFirst={index === 0}
              isLast={index === data.units.length - 1}
              onMove={(direction) => {
                move(unit.id, targetPosition(index, direction));
              }}
              onRename={(name) => rename(unit.id, name)}
              onDelete={() => {
                remove(unit.id);
              }}
              renameLabel={t('actions.newName')}
              serverErrorFields={unitNameErrorFields}
            />
          </li>
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
