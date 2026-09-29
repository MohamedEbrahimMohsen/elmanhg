import { PracticeStart } from '../components/PracticeStart';

export interface PracticePageProps {
  lessonId: string;
}

export function PracticePage({ lessonId }: PracticePageProps) {
  return <PracticeStart lessonId={lessonId} />;
}
