import { useMatch } from '@tanstack/react-router';
import { AvatarDock } from './AvatarDock';

export function StudentAvatarDock() {
  const onAssistantPage = useMatch({ from: '/student/assistant', shouldThrow: false }) !== undefined;
  return onAssistantPage ? null : <AvatarDock />;
}
