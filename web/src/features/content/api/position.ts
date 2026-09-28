export type MoveDirection = 'up' | 'down';

export function targetPosition(index: number, direction: MoveDirection): number {
  return direction === 'up' ? index : index + 2;
}
