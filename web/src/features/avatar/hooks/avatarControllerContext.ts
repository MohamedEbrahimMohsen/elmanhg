import { createContext, type Dispatch } from 'react';
import type { AvatarContextInput } from '../api/avatarContext';
import type { AvatarAction, AvatarState } from './avatarReducer';

export interface AvatarController {
  state: AvatarState;
  open: (context: AvatarContextInput) => void;
  close: () => void;
  dispatch: Dispatch<AvatarAction>;
}

export const AvatarControllerContext = createContext<AvatarController | null>(null);
