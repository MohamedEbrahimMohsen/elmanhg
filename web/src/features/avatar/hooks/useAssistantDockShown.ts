import { useMatch } from '@tanstack/react-router';

export function useAssistantDockShown(): boolean {
  const onAssistantPage = useMatch({ from: '/student/assistant', shouldThrow: false }) !== undefined;
  const takingExam = useMatch({ from: '/student/exam/$sessionId', shouldThrow: false }) !== undefined;
  return !onAssistantPage && !takingExam;
}
