import { useState } from 'react';
import type { SessionItemResult, SessionResult } from '@/shared/api/generated/model';
import { initialPosition, sortedItems } from '../api/quizSession';

export interface QuizNavigation {
  item: SessionItemResult;
  nextItem: SessionItemResult | undefined;
  position: number;
  total: number;
  isLast: boolean;
  focusOnMount: boolean;
  next: () => void;
}

export function useQuizNavigation(session: SessionResult): QuizNavigation {
  const items = sortedItems(session);
  const [position, setPosition] = useState(() => initialPosition(session));
  const [focusOnMount, setFocusOnMount] = useState(false);
  const index = items.findIndex((item) => Number(item.position) === position);
  const item = items[index];
  if (item === undefined) {
    throw new Error('Session position is out of range.');
  }

  return {
    item,
    nextItem: items[index + 1],
    position,
    total: items.length,
    isLast: index === items.length - 1,
    focusOnMount,
    next: () => {
      setPosition(position + 1);
      setFocusOnMount(true);
    },
  };
}
