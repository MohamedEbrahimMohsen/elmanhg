import { useState } from 'react';
import { useGetLessons } from '@/shared/api/generated/lessons/lessons';
import { useGetSubject, useGetSubjects } from '@/shared/api/generated/subjects/subjects';

export interface PickerOption {
  value: string;
  label: string;
}

export function useQuestionLessonPicker() {
  const [subjectId, setSubjectId] = useState('');
  const [unitId, setUnitId] = useState('');
  const [lessonId, setLessonId] = useState('');
  const subjects = useGetSubjects();
  const subject = useGetSubject(subjectId, { query: { enabled: subjectId !== '' } });
  const lessons = useGetLessons({ unitId }, { query: { enabled: unitId !== '' } });
  const failed = [subjects, subject, lessons].find((query) => query.isError);

  return {
    subjectId,
    unitId,
    lessonId,
    subjects: (subjects.data ?? []).map((item): PickerOption => ({ value: item.id, label: item.name })),
    units: subjectId === '' ? [] : (subject.data?.units ?? []).map((item) => ({ value: item.id, label: item.name })),
    lessons: unitId === '' ? [] : (lessons.data ?? []).map((item) => ({ value: item.id, label: item.name })),
    isLoadingSubjects: subjects.isPending,
    isLoadingUnits: subjectId !== '' && subject.isPending,
    isLoadingLessons: unitId !== '' && lessons.isPending,
    hasNoLessons: unitId !== '' && lessons.isSuccess && lessons.data.length === 0,
    error: failed?.error,
    isError: failed !== undefined,
    retry: () => {
      void failed?.refetch();
    },
    chooseSubject: (id: string) => {
      setSubjectId(id);
      setUnitId('');
      setLessonId('');
    },
    chooseUnit: (id: string) => {
      setUnitId(id);
      setLessonId('');
    },
    chooseLesson: setLessonId,
  };
}

export type QuestionLessonPickerState = ReturnType<typeof useQuestionLessonPicker>;
