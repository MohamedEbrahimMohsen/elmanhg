import { use } from 'react';
import { AvatarControllerContext, type AvatarController } from './avatarControllerContext';

export function useAvatar(): AvatarController {
  const controller = use(AvatarControllerContext);
  if (controller === null) {
    throw new Error('useAvatar must be used inside AvatarProvider');
  }
  return controller;
}
