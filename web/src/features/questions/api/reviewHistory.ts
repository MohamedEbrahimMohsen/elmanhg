import type { QuestionDecisionResult, QuestionRevisionEntryResult } from '@/shared/api/generated/model';

export interface ReviewHistoryEntry {
  key: string;
  at: string;
  kind: 'revision' | 'approved' | 'rejected';
  version: number;
  reason: string | null;
  actorName: string | null;
  difficulty: string | null;
  difficultyChangedFrom: string | null;
}

function fromRevision(revision: QuestionRevisionEntryResult): ReviewHistoryEntry {
  const version = Number(revision.version);
  return {
    key: `rev-${String(version)}`,
    at: revision.editedAt,
    kind: 'revision',
    version,
    reason: null,
    actorName: null,
    difficulty: null,
    difficultyChangedFrom: null,
  };
}

function fromDecision(decision: QuestionDecisionResult): ReviewHistoryEntry {
  const version = Number(decision.version);
  return {
    key: `dec-${decision.decidedAt}-${String(version)}`,
    at: decision.decidedAt,
    kind: decision.outcome === 'Rejected' ? 'rejected' : 'approved',
    version,
    reason: decision.reason ?? null,
    actorName: decision.decidedByName ?? null,
    difficulty: decision.difficulty,
    difficultyChangedFrom: decision.difficultyChangedFrom ?? null,
  };
}

export function buildReviewHistory(
  revisions: QuestionRevisionEntryResult[],
  decisions: QuestionDecisionResult[],
): ReviewHistoryEntry[] {
  return [...revisions.map(fromRevision), ...decisions.map(fromDecision)].sort((first, second) => {
    const byTime = new Date(first.at).getTime() - new Date(second.at).getTime();
    if (byTime !== 0) {
      return byTime;
    }
    return Number(second.kind === 'revision') - Number(first.kind === 'revision');
  });
}
