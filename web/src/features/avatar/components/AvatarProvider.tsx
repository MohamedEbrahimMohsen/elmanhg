import { useReducer, type ReactNode } from 'react';
import { AvatarControllerContext } from '../hooks/avatarControllerContext';
import { avatarReducer, initialAvatarState } from '../hooks/avatarReducer';

export function AvatarProvider({ children }: { children: ReactNode }) {
  const [state, dispatch] = useReducer(avatarReducer, initialAvatarState);

  return (
    <AvatarControllerContext
      value={{
        state,
        dispatch,
        open: (context) => {
          dispatch({ type: 'open', context });
        },
        close: () => {
          dispatch({ type: 'close' });
        },
      }}
    >
      {children}
    </AvatarControllerContext>
  );
}
