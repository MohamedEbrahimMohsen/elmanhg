export type MathKeyId =
  | 'd0'
  | 'd1'
  | 'd2'
  | 'd3'
  | 'd4'
  | 'd5'
  | 'd6'
  | 'd7'
  | 'd8'
  | 'd9'
  | 'point'
  | 'plus'
  | 'minus'
  | 'times'
  | 'divide'
  | 'equals'
  | 'openParen'
  | 'closeParen'
  | 'power'
  | 'sqrt'
  | 'fraction'
  | 'pi'
  | 'x'
  | 'y'
  | 'le'
  | 'ge'
  | 'ne'
  | 'pm'
  | 'sin'
  | 'cos'
  | 'tan'
  | 'log'
  | 'text'
  | 'left'
  | 'right'
  | 'backspace';

export type MathKeyAction =
  | { kind: 'insert'; text: string; caret?: number; wraps?: boolean }
  | { kind: 'backspace' }
  | { kind: 'left' }
  | { kind: 'right' };

export interface MathKey {
  id: MathKeyId;
  glyph: string | null;
  action: MathKeyAction;
}

export interface MathEditState {
  value: string;
  selectionStart: number;
  selectionEnd: number;
}

export interface MathEditResult {
  value: string;
  caret: number;
}

function insert(id: MathKeyId, glyph: string | null, text: string, caret?: number): MathKey {
  return {
    id,
    glyph,
    action: caret === undefined ? { kind: 'insert', text } : { kind: 'insert', text, caret, wraps: true },
  };
}

function digit(id: MathKeyId): MathKey {
  const text = id.slice(1);
  return insert(id, text, text);
}

export const mathKeyRows: readonly (readonly MathKey[])[] = [
  [
    digit('d7'),
    digit('d8'),
    digit('d9'),
    insert('divide', '÷', '\\div '),
    insert('openParen', '(', '('),
    insert('closeParen', ')', ')'),
  ],
  [
    digit('d4'),
    digit('d5'),
    digit('d6'),
    insert('times', '×', '\\times '),
    insert('power', 'xⁿ', '^{}', 2),
    insert('sqrt', '√', '\\sqrt{}', 6),
  ],
  [
    digit('d1'),
    digit('d2'),
    digit('d3'),
    insert('minus', '−', '-'),
    insert('fraction', 'a/b', '\\frac{}{}', 6),
    insert('pi', 'π', '\\pi '),
  ],
  [
    digit('d0'),
    insert('point', '.', '.'),
    insert('equals', '=', '='),
    insert('plus', '+', '+'),
    insert('x', 'x', 'x'),
    insert('y', 'y', 'y'),
  ],
  [
    insert('le', '≤', '\\le '),
    insert('ge', '≥', '\\ge '),
    insert('ne', '≠', '\\ne '),
    insert('pm', '±', '\\pm '),
    insert('sin', 'sin', '\\sin '),
    insert('cos', 'cos', '\\cos '),
  ],
  [
    insert('tan', 'tan', '\\tan '),
    insert('log', 'log', '\\log '),
    insert('text', null, '\\text{}', 6),
    { id: 'left', glyph: null, action: { kind: 'left' } },
    { id: 'right', glyph: null, action: { kind: 'right' } },
    { id: 'backspace', glyph: null, action: { kind: 'backspace' } },
  ],
];

const keysById = new Map(mathKeyRows.flat().map((key) => [key.id, key]));

export function applyMathKey(state: MathEditState, keyId: MathKeyId): MathEditResult {
  const { value } = state;
  const start = Math.min(state.selectionStart, state.selectionEnd);
  const end = Math.max(state.selectionStart, state.selectionEnd);
  const key = keysById.get(keyId);
  if (!key) {
    return { value, caret: end };
  }
  const before = value.slice(0, start);
  const selected = value.slice(start, end);
  const after = value.slice(end);
  const { action } = key;
  switch (action.kind) {
    case 'insert': {
      const caret = action.caret ?? action.text.length;
      if (action.wraps && selected !== '') {
        const wrapped = action.text.slice(0, caret) + selected + action.text.slice(caret);
        return { value: before + wrapped + after, caret: start + caret + selected.length };
      }
      return { value: before + action.text + after, caret: start + caret };
    }
    case 'backspace':
      if (selected !== '') {
        return { value: before + after, caret: start };
      }
      return start > 0 ? { value: value.slice(0, start - 1) + after, caret: start - 1 } : { value, caret: 0 };
    case 'left':
      return { value, caret: selected !== '' ? start : Math.max(0, start - 1) };
    case 'right':
      return { value, caret: selected !== '' ? end : Math.min(value.length, end + 1) };
  }
}
