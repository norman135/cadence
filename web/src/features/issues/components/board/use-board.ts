import { keepPreviousData, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { ApiError, errorMessage } from '@/shared/api/api-error';
import {
  getGetBoardQueryKey,
  getListIssuesInfiniteQueryKey,
  getListProjectsQueryKey,
  useGetBoard,
  useMoveCard,
} from '@/shared/api/generated/endpoints';
import type { BoardCard, BoardResponse, GetBoardParams } from '@/shared/api/generated/model';
import { useCurrentOrganization } from '@/shared/workspace';

/** A project's board. Changing quick filters keeps the previous board on screen until the new one loads. */
export function useBoard(projectId: string | undefined, params: GetBoardParams) {
  const { organization } = useCurrentOrganization();
  return useGetBoard(organization?.id ?? '', projectId ?? '', params, {
    query: {
      enabled: !!organization && !!projectId,
      staleTime: 10_000,
      placeholderData: keepPreviousData,
    },
  });
}

/** The board with one card moved to a status and rank, as the server will store it. */
export function applyMove(
  board: BoardResponse,
  card: BoardCard,
  toStatusId: string,
  rank: string,
): BoardResponse {
  const moved = { ...card, statusId: toStatusId, rank };
  return {
    ...board,
    columns: board.columns.map((column) => {
      const without = column.cards.filter((candidate) => candidate.id !== card.id);
      const removed = without.length !== column.cards.length;
      if (column.statusId !== toStatusId) {
        return removed ? { ...column, cards: without, total: column.total - 1 } : column;
      }

      const index = without.findIndex((candidate) => candidate.rank > rank);
      const cards =
        index === -1
          ? [...without, moved]
          : [...without.slice(0, index), moved, ...without.slice(index)];
      return { ...column, cards, total: column.total + (removed ? 0 : 1) };
    }),
  };
}

/**
 * Moves a card optimistically: the board updates at once, the server confirms with the card's new
 * version, and a refusal puts the card back. A 409 means someone else changed the issue first, so
 * the board reloads to show their change.
 */
export function useMoveCardOnBoard(projectId: string, params: GetBoardParams) {
  const { organization } = useCurrentOrganization();
  const organizationId = organization?.id ?? '';
  const queryClient = useQueryClient();
  const queryKey = getGetBoardQueryKey(organizationId, projectId, params);
  const mutation = useMoveCard();

  return async (card: BoardCard, toStatusId: string, rank: string) => {
    if (card.statusId === toStatusId && card.rank === rank) return;

    await queryClient.cancelQueries({ queryKey });
    const previous = queryClient.getQueryData<BoardResponse>(queryKey);
    if (previous) queryClient.setQueryData(queryKey, applyMove(previous, card, toStatusId, rank));

    try {
      const result = await mutation.mutateAsync({
        organizationId,
        projectId,
        data: {
          issueId: card.id,
          fromStatusId: card.statusId,
          toStatusId,
          rank,
          version: card.version,
          assigneeId: card.assignee?.id ?? null,
        },
      });

      // The next move of this card needs its new version.
      queryClient.setQueryData<BoardResponse>(
        queryKey,
        (board) =>
          board && {
            ...board,
            columns: board.columns.map((column) => ({
              ...column,
              cards: column.cards.map((candidate) =>
                candidate.id === card.id ? { ...candidate, version: result.version } : candidate,
              ),
            })),
          },
      );

      // Lists and project counts show statuses too; the board itself is already right.
      const [, listPath] = getListIssuesInfiniteQueryKey(organizationId);
      void queryClient.invalidateQueries({ queryKey: ['infinite', listPath] });
      void queryClient.invalidateQueries({ queryKey: getListProjectsQueryKey(organizationId) });
    } catch (error) {
      if (previous) queryClient.setQueryData(queryKey, previous);
      if (error instanceof ApiError && error.status === 409 && error.code === 'issues.changed') {
        toast.info(`${card.key} changed while you were moving it. The board is up to date now.`);
        void queryClient.invalidateQueries({ queryKey });
      } else {
        toast.error(errorMessage(error));
      }
    }
  };
}
