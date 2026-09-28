import { useTranslation } from 'react-i18next';
import { ContentErrorState, ContentListSkeleton } from '@/features/content';
import { useGetSubjectExamBlueprints } from '@/shared/api/generated/exam-blueprints/exam-blueprints';
import { toFormValues } from '../api/blueprintValues';
import { useBlueprintDelete } from '../hooks/useBlueprintDelete';
import { useBlueprintSave } from '../hooks/useBlueprintSave';
import { BlueprintEditor } from './BlueprintEditor';
import { UnitBlueprintSection } from './UnitBlueprintSection';

export interface SubjectBlueprintsProps {
  subjectId: string;
}

export function SubjectBlueprints({ subjectId }: SubjectBlueprintsProps) {
  const { t } = useTranslation('blueprints');
  const { data, error, isPending, isError, refetch } = useGetSubjectExamBlueprints(subjectId);
  const { saveSubjectDefault, saveUnit } = useBlueprintSave(subjectId);
  const deleteBlueprint = useBlueprintDelete(subjectId);

  if (isPending) {
    return <ContentListSkeleton label={t('page.loading')} />;
  }
  if (isError) {
    return (
      <ContentErrorState
        title={t('page.errorTitle')}
        error={error}
        onRetry={() => {
          void refetch();
        }}
      />
    );
  }

  const { defaultBlueprint } = data;

  return (
    <div className="flex flex-col gap-3">
      <BlueprintEditor
        key={defaultBlueprint?.id ?? 'new'}
        title={t('editor.defaultTitle')}
        caption={defaultBlueprint ? undefined : t('editor.defaultUnsaved')}
        defaults={toFormValues(defaultBlueprint)}
        available={data.servable}
        onSave={saveSubjectDefault}
      />
      {data.units.length === 0 ? <p className="text-ui text-text-muted">{t('page.noUnits')}</p> : null}
      {data.units.map((unit) => (
        <UnitBlueprintSection
          key={unit.unitId}
          unit={unit}
          defaultBlueprint={defaultBlueprint}
          onSave={saveUnit}
          onDelete={deleteBlueprint}
        />
      ))}
    </div>
  );
}
