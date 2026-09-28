import type { ReactNode } from 'react';
import type { Editor } from '@tiptap/react';
import { useEditorState } from '@tiptap/react';
import { Bold, Heading2, ImagePlus, Italic, List, ListOrdered, Sigma } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';

export interface RichTextToolbarProps {
  editor: Editor;
  fieldLabel: string;
  onOpenFormula: () => void;
  onOpenImage: () => void;
}

interface ToolProps {
  label: string;
  pressed?: boolean;
  onClick: () => void;
  children: ReactNode;
}

function Tool({ label, pressed, onClick, children }: ToolProps) {
  return (
    <Button size="sm" variant="secondary" aria-label={label} aria-pressed={pressed} onClick={onClick}>
      {children}
    </Button>
  );
}

export function RichTextToolbar({ editor, fieldLabel, onOpenFormula, onOpenImage }: RichTextToolbarProps) {
  const { t } = useTranslation('content');
  const active = useEditorState({
    editor,
    selector: ({ editor: current }) => ({
      bold: current.isActive('bold'),
      italic: current.isActive('italic'),
      heading: current.isActive('heading', { level: 2 }),
      bulletList: current.isActive('bulletList'),
      orderedList: current.isActive('orderedList'),
    }),
  });
  const run = () => editor.chain().focus();

  return (
    <div
      role="toolbar"
      aria-label={t('lessonEditor.toolbar.label', { field: fieldLabel })}
      className="flex flex-wrap gap-1"
    >
      <Tool label={t('lessonEditor.toolbar.bold')} pressed={active.bold} onClick={() => run().toggleBold().run()}>
        <Bold aria-hidden className="size-4" />
      </Tool>
      <Tool label={t('lessonEditor.toolbar.italic')} pressed={active.italic} onClick={() => run().toggleItalic().run()}>
        <Italic aria-hidden className="size-4" />
      </Tool>
      <Tool
        label={t('lessonEditor.toolbar.heading')}
        pressed={active.heading}
        onClick={() => run().toggleHeading({ level: 2 }).run()}
      >
        <Heading2 aria-hidden className="size-4" />
      </Tool>
      <Tool
        label={t('lessonEditor.toolbar.bulletList')}
        pressed={active.bulletList}
        onClick={() => run().toggleBulletList().run()}
      >
        <List aria-hidden className="size-4" />
      </Tool>
      <Tool
        label={t('lessonEditor.toolbar.orderedList')}
        pressed={active.orderedList}
        onClick={() => run().toggleOrderedList().run()}
      >
        <ListOrdered aria-hidden className="size-4" />
      </Tool>
      <Tool label={t('lessonEditor.toolbar.formula')} onClick={onOpenFormula}>
        <Sigma aria-hidden className="size-4" />
      </Tool>
      <Tool label={t('lessonEditor.toolbar.image')} onClick={onOpenImage}>
        <ImagePlus aria-hidden className="size-4" />
      </Tool>
    </div>
  );
}
