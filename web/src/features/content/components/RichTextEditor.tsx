import 'katex/dist/katex.min.css';
import { useState } from 'react';
import { Image } from '@tiptap/extension-image';
import { Mathematics } from '@tiptap/extension-mathematics';
import { EditorContent, useEditor } from '@tiptap/react';
import { StarterKit } from '@tiptap/starter-kit';
import { useTranslation } from 'react-i18next';
import { Dialog, DialogContent } from '@/shared/ui/dialog';
import { FormulaInsertForm } from './FormulaInsertForm';
import { ImageInsertForm } from './ImageInsertForm';
import { RichTextToolbar } from './RichTextToolbar';

export interface RichTextEditorProps {
  value: string;
  onChange: (html: string) => void;
  onBlur: () => void;
  labelledBy: string;
  describedBy?: string | undefined;
  invalid: boolean;
  fieldLabel: string;
  onUploadImage: (file: File) => Promise<string>;
}

type OpenDialog = 'formula' | 'image' | null;

export function RichTextEditor({
  value,
  onChange,
  onBlur,
  labelledBy,
  describedBy,
  invalid,
  fieldLabel,
  onUploadImage,
}: RichTextEditorProps) {
  const { t } = useTranslation('content');
  const [dialog, setDialog] = useState<OpenDialog>(null);
  const editor = useEditor(
    {
      extensions: [
        StarterKit.configure({ heading: { levels: [2, 3] }, link: { openOnClick: false } }),
        Image.configure({ allowBase64: false }),
        Mathematics.configure({ katexOptions: { throwOnError: false } }),
      ],
      content: value,
      editorProps: {
        attributes: {
          role: 'textbox',
          'aria-multiline': 'true',
          'aria-labelledby': labelledBy,
          ...(describedBy ? { 'aria-describedby': describedBy } : {}),
          'aria-invalid': String(invalid),
          class:
            'rich-text min-h-36 rounded-sm border border-border-strong bg-surface px-3 py-2.5 focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden aria-invalid:border-danger',
        },
      },
      onUpdate: ({ editor: current }) => {
        onChange(current.getHTML());
      },
      onBlur: () => {
        onBlur();
      },
    },
    [invalid, describedBy],
  );
  const closeDialog = () => {
    setDialog(null);
  };
  const onOpenChange = (open: boolean) => {
    if (!open) {
      closeDialog();
    }
  };

  return (
    <div className="flex flex-col gap-2">
      <RichTextToolbar
        editor={editor}
        fieldLabel={fieldLabel}
        onOpenFormula={() => {
          setDialog('formula');
        }}
        onOpenImage={() => {
          setDialog('image');
        }}
      />
      <EditorContent editor={editor} />
      <Dialog open={dialog === 'formula'} onOpenChange={onOpenChange}>
        <DialogContent title={t('lessonEditor.formula.title')}>
          <FormulaInsertForm
            onInsert={({ latex, block }) => {
              if (block) {
                editor.chain().focus().insertBlockMath({ latex }).run();
              } else {
                editor.chain().focus().insertInlineMath({ latex }).run();
              }
              closeDialog();
            }}
            onCancel={closeDialog}
          />
        </DialogContent>
      </Dialog>
      <Dialog open={dialog === 'image'} onOpenChange={onOpenChange}>
        <DialogContent title={t('lessonEditor.image.title')}>
          <ImageInsertForm
            onUpload={onUploadImage}
            onInsert={(src, alt) => {
              editor.chain().focus().setImage({ src, alt }).run();
              closeDialog();
            }}
            onCancel={closeDialog}
          />
        </DialogContent>
      </Dialog>
    </div>
  );
}
