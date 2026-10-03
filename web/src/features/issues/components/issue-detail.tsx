import { Ellipsis, Link2, Plus, Trash2, X } from 'lucide-react';
import { lazy, Suspense, useState, type ReactNode } from 'react';
import { Link, useNavigate } from 'react-router';
import { toast } from 'sonner';
import {
  useCreateIssueLink,
  useDeleteIssue,
  useDeleteIssueLink,
  useGetIssue,
  useUpdateIssue,
} from '@/shared/api/generated/endpoints';
import type {
  IssueDetail as IssueDetailModel,
  IssueLinkType,
  UpdateIssueRequest,
} from '@/shared/api/generated/model';
import { ApiError, errorMessage } from '@/shared/api/api-error';
import { cn } from '@/shared/lib/utils';
import { Badge } from '@/shared/ui/badge';
import { Button } from '@/shared/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/shared/ui/dropdown-menu';
import { Input } from '@/shared/ui/input';
import { Select } from '@/shared/ui/select';
import { Skeleton } from '@/shared/ui/skeleton';
import { StatusIcon } from '@/shared/ui/work-glyphs';
import { useCurrentOrganization, useCurrentUser, useRefreshIssues } from '@/shared/workspace';
import { dueTone, formatDate, statusOf } from '../lib/options';
import { Comments } from './comments';
import { Markdown } from './markdown';
import {
  AssigneePicker,
  EstimatePicker,
  LabelsPicker,
  PriorityPicker,
  StatusPicker,
  TypePicker,
} from './pickers';

const CreateIssueDialog = lazy(() => import('../dialogs/create-issue-dialog'));

const RELATIONS: Record<string, string> = {
  blocks: 'Blocks',
  blocked_by: 'Blocked by',
  relates_to: 'Relates to',
  duplicates: 'Duplicates',
  duplicated_by: 'Duplicated by',
};

/**
 * An issue as a document first and a form second (design board 08). Every field edits in place;
 * changes save immediately and refresh the lists.
 */
export function IssueDetail({
  issueKey,
  inDrawer = false,
}: {
  issueKey: string;
  inDrawer?: boolean;
}) {
  const { organization } = useCurrentOrganization();
  const organizationId = organization?.id ?? '';
  const {
    data: issue,
    isPending,
    error,
  } = useGetIssue(organizationId, issueKey, {
    query: { enabled: !!organization },
  });

  if (isPending) {
    return (
      <div className="flex flex-col gap-4" aria-busy="true">
        <Skeleton className="h-5 w-24" />
        <Skeleton className="h-8 w-2/3" />
        <Skeleton className="h-24" />
      </div>
    );
  }

  if (!issue) {
    return (
      <div className="py-16 text-center">
        <p className="font-semibold">Issue not found</p>
        <p className="mt-1 text-sm text-muted-foreground">
          {error instanceof ApiError && error.status === 404
            ? `${issueKey.toUpperCase()} doesn't exist, or you don't have access to its project.`
            : errorMessage(error)}
        </p>
      </div>
    );
  }

  return <LoadedIssue issue={issue} inDrawer={inDrawer} />;
}

function LoadedIssue({ issue, inDrawer }: { issue: IssueDetailModel; inDrawer: boolean }) {
  const { organization } = useCurrentOrganization();
  const { data: me } = useCurrentUser();
  const organizationId = organization?.id ?? '';
  const refresh = useRefreshIssues();
  const navigate = useNavigate();
  const update = useUpdateIssue();
  const remove = useDeleteIssue();
  const [creatingSubIssue, setCreatingSubIssue] = useState(false);

  const canWrite = issue.role === 'Member' || issue.role === 'Admin';
  const isAdmin = issue.role === 'Admin';
  const canDelete = isAdmin || (canWrite && issue.reporter.id === me?.id);

  const change = async (data: UpdateIssueRequest) => {
    try {
      await update.mutateAsync({ organizationId, issueId: issue.id, data });
      await refresh(issue.key);
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const destroy = async () => {
    try {
      await remove.mutateAsync({ organizationId, issueId: issue.id });
      await refresh();
      toast.success(`${issue.key} deleted`);
      await navigate(`/${organization?.slug ?? ''}`, { replace: true });
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  return (
    <div className={cn('grid gap-8', !inDrawer && 'lg:grid-cols-[minmax(0,1fr)_280px]')}>
      <article className="flex min-w-0 flex-col gap-6">
        <div className="flex flex-col gap-2">
          <div className="flex items-center gap-2 text-[13px] text-muted-foreground">
            <span className="font-mono">{issue.key}</span>
            <span aria-hidden="true">·</span>
            <span>{issue.projectName}</span>
            {issue.parent && (
              <>
                <span aria-hidden="true">·</span>
                <span className="flex min-w-0 items-center gap-1.5">
                  in
                  <Link
                    to={`/${organization?.slug ?? ''}/issues/${issue.parent.key}`}
                    className="truncate font-medium text-foreground hover:underline"
                  >
                    {issue.parent.key} {issue.parent.title}
                  </Link>
                  {canWrite && (
                    <button
                      type="button"
                      aria-label="Remove from parent"
                      className="cursor-default rounded p-0.5 hover:bg-accent"
                      onClick={() => void change({ clear: ['Parent'] })}
                    >
                      <X className="size-3.5" />
                    </button>
                  )}
                </span>
              </>
            )}
            {canDelete && (
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <Button
                    variant="ghost"
                    size="icon-sm"
                    className="ml-auto"
                    aria-label="Issue actions"
                  >
                    <Ellipsis />
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end">
                  <DropdownMenuItem variant="destructive" onSelect={() => void destroy()}>
                    <Trash2 />
                    Delete issue
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            )}
          </div>
          <EditableTitle
            key={issue.title}
            title={issue.title}
            disabled={!canWrite}
            onSave={(title) => void change({ title })}
          />
        </div>

        <EditableDescription
          key={issue.description ?? ''}
          description={issue.description ?? ''}
          disabled={!canWrite}
          onSave={(description) =>
            void change(description ? { description } : { clear: ['Description'] })
          }
        />

        {inDrawer && <Properties issue={issue} canWrite={canWrite} onChange={change} />}

        <Section
          title="Sub-issues"
          count={issue.subIssues.length}
          action={
            canWrite && (
              <Button
                variant="ghost"
                size="sm"
                onClick={() => {
                  setCreatingSubIssue(true);
                }}
              >
                <Plus />
                Add
              </Button>
            )
          }
        >
          {issue.subIssues.length > 0 ? (
            <ul className="divide-y overflow-hidden rounded-lg border">
              {issue.subIssues.map((child) => (
                <li key={child.id}>
                  <Link
                    to={`/${organization?.slug ?? ''}/issues/${child.key}`}
                    className="flex h-9 items-center gap-3 px-3 text-sm hover:bg-accent/50"
                  >
                    <StatusIcon category={statusOf(child.status).glyph} />
                    <span className="w-16 font-mono text-xs text-muted-foreground">
                      {child.key}
                    </span>
                    <span className="truncate">{child.title}</span>
                  </Link>
                </li>
              ))}
            </ul>
          ) : (
            <p className="text-[13px] text-muted-foreground">No sub-issues.</p>
          )}
        </Section>

        <LinksSection issue={issue} canWrite={canWrite} />

        <Comments issueId={issue.id} issueKey={issue.key} canWrite={canWrite} isAdmin={isAdmin} />
      </article>

      {!inDrawer && (
        <aside className="flex flex-col gap-1 self-start lg:sticky lg:top-20">
          <Properties issue={issue} canWrite={canWrite} onChange={change} />
        </aside>
      )}

      {creatingSubIssue && (
        <Suspense fallback={null}>
          <CreateIssueDialog
            open
            projectId={issue.projectId}
            parent={{ id: issue.id, key: issue.key }}
            onOpenChange={setCreatingSubIssue}
          />
        </Suspense>
      )}
    </div>
  );
}

function Properties({
  issue,
  canWrite,
  onChange,
}: {
  issue: IssueDetailModel;
  canWrite: boolean;
  onChange: (data: UpdateIssueRequest) => Promise<void>;
}) {
  const disabled = !canWrite;
  return (
    <dl className="grid grid-cols-[88px_minmax(0,1fr)] items-center gap-x-3 gap-y-1 text-[13px] [&_dt]:text-muted-foreground">
      <dt>Status</dt>
      <dd>
        <StatusPicker
          value={issue.status}
          disabled={disabled}
          onChange={(status) => void onChange({ status })}
        />
      </dd>
      <dt>Priority</dt>
      <dd>
        <PriorityPicker
          value={issue.priority}
          disabled={disabled}
          onChange={(priority) => void onChange({ priority })}
        />
      </dd>
      <dt>Assignee</dt>
      <dd>
        <AssigneePicker
          projectId={issue.projectId}
          value={issue.assignee}
          disabled={disabled}
          onChange={(assigneeId) =>
            void onChange(assigneeId ? { assigneeId } : { clear: ['Assignee'] })
          }
        />
      </dd>
      <dt>Estimate</dt>
      <dd>
        <EstimatePicker
          value={issue.estimate}
          disabled={disabled}
          onChange={(estimate) =>
            void onChange(estimate != null ? { estimate } : { clear: ['Estimate'] })
          }
        />
      </dd>
      <dt>Due date</dt>
      <dd className="flex items-center gap-2">
        <input
          type="date"
          aria-label="Due date"
          disabled={disabled}
          value={issue.dueDate ?? ''}
          onChange={(event) =>
            void onChange(
              event.target.value ? { dueDate: event.target.value } : { clear: ['DueDate'] },
            )
          }
          className="h-7 rounded-md border border-transparent bg-transparent px-1 text-[13px] outline-none hover:border-input focus-visible:border-primary"
        />
        {issue.dueDate && issue.status !== 'Done' && issue.status !== 'Canceled' && (
          <Badge variant={dueTone(issue.dueDate)} className="h-5">
            {dueTone(issue.dueDate) === 'destructive'
              ? 'Overdue'
              : dueTone(issue.dueDate) === 'warning'
                ? 'Soon'
                : 'Due'}
          </Badge>
        )}
      </dd>
      <dt>Labels</dt>
      <dd>
        <LabelsPicker
          value={issue.labels}
          disabled={disabled}
          onChange={(labelIds) => void onChange({ labelIds })}
        />
      </dd>
      <dt>Type</dt>
      <dd>
        <TypePicker
          value={issue.type}
          disabled={disabled}
          onChange={(type) => void onChange({ type })}
        />
      </dd>
      <dd className="col-span-2 mt-3 border-t pt-3 text-xs text-muted-foreground">
        Reported by {issue.reporter.displayName} · {formatDate(issue.createdAt)}
        <br />
        Updated {formatDate(issue.updatedAt)}
      </dd>
    </dl>
  );
}

function Section({
  title,
  count,
  action,
  children,
}: {
  title: string;
  count?: number;
  action?: ReactNode;
  children: ReactNode;
}) {
  return (
    <section className="flex flex-col gap-2">
      <div className="flex items-center gap-2">
        <h2 className="text-sm font-semibold">{title}</h2>
        {count != null && count > 0 && (
          <span className="text-sm text-subtle-foreground">{count}</span>
        )}
        <span className="flex-1" />
        {action}
      </div>
      {children}
    </section>
  );
}

function EditableTitle({
  title,
  disabled,
  onSave,
}: {
  title: string;
  disabled: boolean;
  onSave: (title: string) => void;
}) {
  const [value, setValue] = useState(title);
  const commit = () => {
    const next = value.trim();
    if (next && next !== title) onSave(next);
    else setValue(title);
  };

  return (
    <textarea
      aria-label="Title"
      rows={1}
      readOnly={disabled}
      value={value}
      onChange={(event) => {
        setValue(event.target.value.replace(/\n/g, ''));
      }}
      onBlur={commit}
      onKeyDown={(event) => {
        if (event.key === 'Enter') {
          event.preventDefault();
          event.currentTarget.blur();
        } else if (event.key === 'Escape') {
          setValue(title);
        }
      }}
      className="field-sizing-content w-full resize-none rounded-md bg-transparent text-[26px] leading-8 font-semibold tracking-[-0.022em] outline-none focus-visible:ring-[3px] focus-visible:ring-ring"
    />
  );
}

function EditableDescription({
  description,
  disabled,
  onSave,
}: {
  description: string;
  disabled: boolean;
  onSave: (description: string) => void;
}) {
  const [editing, setEditing] = useState<string | null>(null);

  if (editing !== null) {
    return (
      <div className="flex flex-col gap-2">
        <textarea
          aria-label="Description"
          rows={8}
          autoFocus
          value={editing}
          onChange={(event) => {
            setEditing(event.target.value);
          }}
          placeholder="Describe the issue. Markdown works."
          className="w-full resize-y rounded-md border border-input bg-card px-3 py-2 font-mono text-[13px] leading-6 outline-none focus-visible:border-primary focus-visible:ring-[3px] focus-visible:ring-ring"
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
            onClick={() => {
              onSave(editing.trim());
              setEditing(null);
            }}
          >
            Save
          </Button>
        </div>
      </div>
    );
  }

  if (!description) {
    return disabled ? (
      <p className="text-sm text-muted-foreground">No description.</p>
    ) : (
      <button
        type="button"
        onClick={() => {
          setEditing('');
        }}
        className="cursor-text self-start text-sm text-muted-foreground hover:text-foreground"
      >
        Add a description…
      </button>
    );
  }

  return (
    <div className="group relative">
      <Markdown source={description} />
      {!disabled && (
        <Button
          variant="ghost"
          size="sm"
          className="mt-1"
          onClick={() => {
            setEditing(description);
          }}
        >
          Edit description
        </Button>
      )}
    </div>
  );
}

function LinksSection({ issue, canWrite }: { issue: IssueDetailModel; canWrite: boolean }) {
  const { organization } = useCurrentOrganization();
  const organizationId = organization?.id ?? '';
  const refresh = useRefreshIssues();
  const create = useCreateIssueLink();
  const remove = useDeleteIssueLink();
  const [adding, setAdding] = useState(false);
  const [type, setType] = useState<IssueLinkType>('RelatesTo');
  const [target, setTarget] = useState('');

  const add = async () => {
    try {
      await create.mutateAsync({
        organizationId,
        issueId: issue.id,
        data: { targetKey: target.trim(), type },
      });
      setTarget('');
      setAdding(false);
      await refresh(issue.key);
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  const unlink = async (linkId: string) => {
    try {
      await remove.mutateAsync({ organizationId, issueId: issue.id, linkId });
      await refresh(issue.key);
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  return (
    <Section
      title="Links"
      count={issue.links.length}
      action={
        canWrite &&
        !adding && (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => {
              setAdding(true);
            }}
          >
            <Link2 />
            Link
          </Button>
        )
      }
    >
      {issue.links.length > 0 && (
        <ul className="flex flex-col gap-1">
          {issue.links.map((link) => (
            <li key={link.id} className="flex items-center gap-2 text-sm">
              <span className="w-24 shrink-0 text-[13px] text-muted-foreground">
                {RELATIONS[link.relation] ?? link.relation}
              </span>
              <StatusIcon category={statusOf(link.issue.status).glyph} />
              <Link
                to={`/${organization?.slug ?? ''}/issues/${link.issue.key}`}
                className="flex min-w-0 items-center gap-2 hover:underline"
              >
                <span className="font-mono text-xs text-muted-foreground">{link.issue.key}</span>
                <span className="truncate">{link.issue.title}</span>
              </Link>
              {canWrite && (
                <Button
                  variant="ghost"
                  size="icon-sm"
                  className="ml-auto"
                  aria-label={`Remove link to ${link.issue.key}`}
                  onClick={() => void unlink(link.id)}
                >
                  <X />
                </Button>
              )}
            </li>
          ))}
        </ul>
      )}
      {issue.links.length === 0 && !adding && (
        <p className="text-[13px] text-muted-foreground">No links.</p>
      )}
      {adding && (
        <form
          className="flex flex-wrap items-center gap-2"
          onSubmit={(event) => {
            event.preventDefault();
            void add();
          }}
        >
          <Select
            aria-label="Link type"
            value={type}
            onChange={(event) => {
              setType(event.target.value as IssueLinkType);
            }}
          >
            <option value="Blocks">Blocks</option>
            <option value="RelatesTo">Relates to</option>
            <option value="Duplicates">Duplicates</option>
          </Select>
          <Input
            aria-label="Issue key"
            placeholder="ATL-142"
            value={target}
            onChange={(event) => {
              setTarget(event.target.value);
            }}
            className="w-32 font-mono"
          />
          <Button type="submit" size="sm" disabled={!target.trim() || create.isPending}>
            Add link
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => {
              setAdding(false);
            }}
          >
            Cancel
          </Button>
        </form>
      )}
    </Section>
  );
}
