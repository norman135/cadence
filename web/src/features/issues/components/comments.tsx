import { useQueryClient } from '@tanstack/react-query';
import { Ellipsis, History, Pencil, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { toast } from 'sonner';
import {
  getListCommentsInfiniteQueryKey,
  useCreateComment,
  useDeleteComment,
  useEditComment,
  useListCommentRevisions,
  useListCommentsInfinite,
} from '@/shared/api/generated/endpoints';
import type { CommentResponse } from '@/shared/api/generated/model';
import { errorMessage } from '@/shared/api/api-error';
import { Avatar } from '@/shared/ui/avatar';
import { Button } from '@/shared/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogTitle } from '@/shared/ui/dialog';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/shared/ui/dropdown-menu';
import { Kbd } from '@/shared/ui/kbd';
import { useCurrentOrganization, useCurrentUser, useRefreshIssues } from '@/shared/workspace';
import { relativeTime } from '../lib/options';
import { Markdown } from './markdown';

const textarea =
  'w-full resize-y rounded-md border border-input bg-card px-3 py-2 text-sm leading-6 outline-none placeholder:text-subtle-foreground focus-visible:border-primary focus-visible:ring-[3px] focus-visible:ring-ring';

/** An issue's discussion (design board 08): comments oldest first, then the composer. */
export function Comments({
  issueId,
  issueKey,
  canWrite,
  isAdmin,
}: {
  issueId: string;
  issueKey: string;
  canWrite: boolean;
  isAdmin: boolean;
}) {
  const { organization } = useCurrentOrganization();
  const { data: me } = useCurrentUser();
  const queryClient = useQueryClient();
  const refresh = useRefreshIssues();
  const organizationId = organization?.id ?? '';

  const comments = useListCommentsInfinite(organizationId, issueId, undefined, {
    query: {
      enabled: !!organization,
      initialPageParam: undefined,
      getNextPageParam: (page) => page.nextCursor ?? undefined,
    },
  });

  const reload = async () => {
    await queryClient.invalidateQueries({
      queryKey: getListCommentsInfiniteQueryKey(organizationId, issueId),
    });
    await refresh(issueKey);
  };

  const create = useCreateComment();
  const [draft, setDraft] = useState('');

  const submit = async () => {
    if (!draft.trim()) return;
    try {
      await create.mutateAsync({ organizationId, issueId, data: { body: draft } });
      setDraft('');
      await reload();
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const items = comments.data?.pages.flatMap((page) => page.items) ?? [];

  return (
    <section aria-labelledby="comments-heading" className="flex flex-col gap-3">
      <h2 id="comments-heading" className="text-sm font-semibold">
        Comments
      </h2>

      {items.map((comment) => (
        <CommentCard
          key={comment.id}
          comment={comment}
          issueId={issueId}
          mine={comment.author.id === me?.id}
          canDelete={canWrite && (comment.author.id === me?.id || isAdmin)}
          onChanged={reload}
        />
      ))}
      {comments.hasNextPage && (
        <Button
          variant="ghost"
          size="sm"
          className="self-start"
          onClick={() => void comments.fetchNextPage()}
        >
          Show more comments
        </Button>
      )}

      {canWrite && (
        <form
          className="flex flex-col gap-2"
          onSubmit={(event) => {
            event.preventDefault();
            void submit();
          }}
        >
          <label htmlFor="new-comment" className="sr-only">
            Leave a comment
          </label>
          <textarea
            id="new-comment"
            rows={3}
            value={draft}
            placeholder="Leave a comment… Markdown works."
            className={textarea}
            onChange={(event) => {
              setDraft(event.target.value);
            }}
            onKeyDown={(event) => {
              if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
                event.preventDefault();
                void submit();
              }
            }}
          />
          <div className="flex items-center justify-end gap-3">
            <span className="flex items-center gap-1 text-xs text-muted-foreground max-sm:hidden">
              <Kbd>Ctrl</Kbd>
              <Kbd>↵</Kbd> to send
            </span>
            <Button type="submit" size="sm" disabled={!draft.trim() || create.isPending}>
              Comment
            </Button>
          </div>
        </form>
      )}
    </section>
  );
}

function CommentCard({
  comment,
  issueId,
  mine,
  canDelete,
  onChanged,
}: {
  comment: CommentResponse;
  issueId: string;
  mine: boolean;
  canDelete: boolean;
  onChanged: () => Promise<void>;
}) {
  const { organization } = useCurrentOrganization();
  const organizationId = organization?.id ?? '';
  const edit = useEditComment();
  const remove = useDeleteComment();
  const [editing, setEditing] = useState<string | null>(null);
  const [historyOpen, setHistoryOpen] = useState(false);

  const save = async () => {
    if (editing === null) return;
    try {
      await edit.mutateAsync({
        organizationId,
        issueId,
        commentId: comment.id,
        data: { body: editing },
      });
      setEditing(null);
      await onChanged();
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const destroy = async () => {
    try {
      await remove.mutateAsync({ organizationId, issueId, commentId: comment.id });
      await onChanged();
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  return (
    <article className="rounded-lg border px-3.5 py-3">
      <header className="flex items-center gap-2 text-[13px]">
        <Avatar name={comment.author.displayName} className="size-5 text-[9px]" />
        <span className="font-medium">{comment.author.displayName}</span>
        <time className="text-muted-foreground" dateTime={comment.createdAt}>
          {relativeTime(comment.createdAt)}
        </time>
        {comment.editedAt && (
          <button
            type="button"
            className="text-muted-foreground underline-offset-2 hover:underline"
            onClick={() => {
              setHistoryOpen(true);
            }}
          >
            (edited)
          </button>
        )}
        {(mine || canDelete) && editing === null && (
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button
                variant="ghost"
                size="icon-sm"
                className="ml-auto"
                aria-label="Comment actions"
              >
                <Ellipsis />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              {mine && (
                <DropdownMenuItem
                  onSelect={() => {
                    setEditing(comment.body);
                  }}
                >
                  <Pencil />
                  Edit
                </DropdownMenuItem>
              )}
              {comment.revisionCount > 0 && (
                <DropdownMenuItem
                  onSelect={() => {
                    setHistoryOpen(true);
                  }}
                >
                  <History />
                  View history
                </DropdownMenuItem>
              )}
              {canDelete && (
                <DropdownMenuItem variant="destructive" onSelect={() => void destroy()}>
                  <Trash2 />
                  Delete
                </DropdownMenuItem>
              )}
            </DropdownMenuContent>
          </DropdownMenu>
        )}
      </header>

      {editing === null ? (
        <Markdown source={comment.body} className="mt-1.5 text-sm leading-[21px]" />
      ) : (
        <div className="mt-2 flex flex-col gap-2">
          <textarea
            aria-label="Edit comment"
            rows={3}
            value={editing}
            className={textarea}
            onChange={(event) => {
              setEditing(event.target.value);
            }}
          />
          <div className="flex justify-end gap-2">
            <Button
              variant="secondary"
              size="sm"
              onClick={() => {
                setEditing(null);
              }}
            >
              Cancel
            </Button>
            <Button
              size="sm"
              disabled={!editing.trim() || edit.isPending}
              onClick={() => void save()}
            >
              Save
            </Button>
          </div>
        </div>
      )}

      {historyOpen && (
        <CommentHistory
          issueId={issueId}
          comment={comment}
          onClose={() => {
            setHistoryOpen(false);
          }}
        />
      )}
    </article>
  );
}

function CommentHistory({
  issueId,
  comment,
  onClose,
}: {
  issueId: string;
  comment: CommentResponse;
  onClose: () => void;
}) {
  const { organization } = useCurrentOrganization();
  const { data: revisions } = useListCommentRevisions(organization?.id ?? '', issueId, comment.id);

  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
    >
      <DialogContent className="max-w-xl">
        <DialogTitle>Edit history</DialogTitle>
        <DialogDescription>Earlier versions of this comment, newest first.</DialogDescription>
        <ol className="mt-4 flex max-h-96 flex-col gap-3 overflow-y-auto">
          {revisions?.map((revision) => (
            <li key={revision.id} className="rounded-lg border bg-sunken px-3 py-2">
              <p className="mb-1 text-xs text-muted-foreground">
                Written {relativeTime(revision.writtenAt)}, replaced{' '}
                {relativeTime(revision.replacedAt)}
              </p>
              <Markdown source={revision.body} className="text-sm leading-[21px]" />
            </li>
          ))}
        </ol>
      </DialogContent>
    </Dialog>
  );
}
