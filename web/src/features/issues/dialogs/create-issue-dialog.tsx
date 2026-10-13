import { useState } from 'react';
import { useNavigate } from 'react-router';
import { toast } from 'sonner';
import { useCreateIssue } from '@/shared/api/generated/endpoints';
import type { IssuePriority, IssueType, LabelResponse } from '@/shared/api/generated/model';
import { errorMessage } from '@/shared/api/api-error';
import { Alert } from '@/shared/ui/alert';
import { Button } from '@/shared/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogTitle } from '@/shared/ui/dialog';
import { Kbd } from '@/shared/ui/kbd';
import { Select } from '@/shared/ui/select';
import {
  useCurrentOrganization,
  useLabels,
  useProjectPeople,
  useProjects,
  useRefreshIssues,
} from '@/shared/workspace';
import {
  AssigneePicker,
  LabelsPicker,
  PriorityPicker,
  StatusPicker,
  TypePicker,
} from '../components/pickers';

/**
 * Creates an issue: a title, and optionally everything else (Ctrl+Enter submits). Opened from the
 * top bar, the command palette and an issue's "Add sub-issue". Loaded on first use.
 */
export default function CreateIssueDialog({
  open,
  onOpenChange,
  projectId: fixedProjectId,
  statusId: initialStatusId,
  parent,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  projectId?: string | undefined;
  /** Start in this status (a board column's "+"); otherwise the workflow's default. */
  statusId?: string | undefined;
  parent?: { id: string; key: string } | undefined;
}) {
  const { organization } = useCurrentOrganization();
  const { data: projects } = useProjects();
  const writable = (projects ?? []).filter(
    (project) => project.archivedAt == null && project.role !== 'Viewer',
  );
  const create = useCreateIssue();
  const refresh = useRefreshIssues();
  const navigate = useNavigate();

  const [projectId, setProjectId] = useState(fixedProjectId ?? writable[0]?.id ?? '');
  const [title, setTitle] = useState('');
  const [description, setDescription] = useState('');
  const [type, setType] = useState<IssueType>('Task');
  const [statusId, setStatusId] = useState<string | null>(initialStatusId ?? null);
  const [priority, setPriority] = useState<IssuePriority>('None');
  const [assigneeId, setAssigneeId] = useState<string | null>(null);
  const [labelIds, setLabelIds] = useState<string[]>([]);
  const { data: people } = useProjectPeople(projectId || undefined);
  const { data: allLabels } = useLabels();

  const assigneePerson = people?.find((person) => person.userId === assigneeId);
  const assignee = assigneePerson
    ? { id: assigneePerson.userId, displayName: assigneePerson.displayName }
    : null;
  const labels: LabelResponse[] = (allLabels ?? []).filter((label) => labelIds.includes(label.id));
  const [failure, setFailure] = useState<string | null>(null);

  const submit = async () => {
    if (!organization || !title.trim() || !projectId) return;
    setFailure(null);
    try {
      const created = await create.mutateAsync({
        organizationId: organization.id,
        data: {
          projectId,
          title,
          description: description || null,
          type,
          statusId,
          priority,
          assigneeId,
          labelIds,
          parentId: parent?.id ?? null,
        },
      });
      await refresh(parent?.key);
      onOpenChange(false);
      toast.success(`${created.key} created`, {
        action: {
          label: 'Open',
          onClick: () => void navigate(`/${organization.slug}/issues/${created.key}`),
        },
      });
    } catch (error) {
      setFailure(errorMessage(error));
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl">
        <DialogTitle>{parent ? `New sub-issue of ${parent.key}` : 'New issue'}</DialogTitle>
        <DialogDescription className="sr-only">
          Give the issue a title, and optionally a description and properties.
        </DialogDescription>
        <form
          className="mt-4 flex flex-col gap-3"
          onSubmit={(event) => {
            event.preventDefault();
            void submit();
          }}
          onKeyDown={(event) => {
            if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
              event.preventDefault();
              void submit();
            }
          }}
        >
          {failure && <Alert variant="destructive">{failure}</Alert>}
          {!fixedProjectId && (
            <Select
              aria-label="Project"
              value={projectId}
              onChange={(event) => {
                setProjectId(event.target.value);
                setStatusId(null);
                setAssigneeId(null);
              }}
              className="self-start"
            >
              {writable.map((project) => (
                <option key={project.id} value={project.id}>
                  {project.key} · {project.name}
                </option>
              ))}
            </Select>
          )}
          <input
            aria-label="Title"
            autoFocus
            placeholder="Issue title"
            value={title}
            maxLength={200}
            onChange={(event) => {
              setTitle(event.target.value);
            }}
            className="w-full bg-transparent text-lg font-semibold tracking-[-0.01em] outline-none placeholder:text-subtle-foreground"
          />
          <textarea
            aria-label="Description"
            rows={5}
            placeholder="Add a description… Markdown works."
            value={description}
            onChange={(event) => {
              setDescription(event.target.value);
            }}
            className="w-full resize-y bg-transparent text-sm leading-6 outline-none placeholder:text-subtle-foreground"
          />
          <div className="flex flex-wrap items-center gap-x-4 gap-y-1 border-t pt-3">
            <StatusPicker projectId={projectId} value={statusId} onChange={setStatusId} />
            <PriorityPicker value={priority} onChange={setPriority} />
            <TypePicker value={type} onChange={setType} />
            {projectId && (
              <AssigneePicker projectId={projectId} value={assignee} onChange={setAssigneeId} />
            )}
            <LabelsPicker value={labels} onChange={setLabelIds} />
          </div>
          <div className="flex items-center justify-end gap-3">
            <span className="flex items-center gap-1 text-xs text-muted-foreground max-sm:hidden">
              <Kbd>Ctrl</Kbd>
              <Kbd>↵</Kbd> to create
            </span>
            <Button type="submit" disabled={!title.trim() || !projectId || create.isPending}>
              Create issue
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
