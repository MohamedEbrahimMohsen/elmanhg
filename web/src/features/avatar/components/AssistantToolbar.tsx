import { History, Plus } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Button } from '@/shared/ui/button';

export interface AssistantToolbarProps {
  listId: string;
  listOpen: boolean;
  newChatDisabled: boolean;
  onShowList: () => void;
  onNewChat: () => void;
}

export function AssistantToolbar({ listId, listOpen, newChatDisabled, onShowList, onNewChat }: AssistantToolbarProps) {
  const { t } = useTranslation();

  return (
    <div className="flex items-center gap-2">
      <Button
        variant="secondary"
        className="lg:hidden"
        aria-expanded={listOpen}
        aria-controls={listId}
        onClick={onShowList}
      >
        <History aria-hidden className="size-4" />
        {t('avatar:panel.history')}
      </Button>
      <Button variant="secondary" disabled={newChatDisabled} onClick={onNewChat}>
        <Plus aria-hidden className="size-4" />
        {t('assistant:newChat')}
      </Button>
    </div>
  );
}
