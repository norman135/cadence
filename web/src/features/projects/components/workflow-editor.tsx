import { useQueryClient } from '@tanstack/react-query';
import { ArrowDown, ArrowUp, Plus, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { toast } from 'sonner';
import { errorMessage } from '@/shared/api/api-error';
import { getGetWorkflowQueryKey, useUpdateWorkflow } from '@/shared/api/generated/endpoints';
import type {
  IssueStatus,
  ProjectResponse,
  TransitionActor,
  WorkflowResponse,
} from '@/shared/api/generated/model';
import { Alert } from '@/shared/ui/alert';
import { Button } from '@/shared/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/card';
import { Input } from '@/shared/ui/input';
import { Select } from '@/shared/ui/select';
import { Switch } from '@/shared/ui/switch';
import { CATEGORY_GLYPHS } from '@/shared/ui/status-categories';
import { StatusIcon } from '@/shared/ui/work-glyphs';
import {
  isBoardQuery,
  useCurrentOrganization,
  useRefreshIssues,
  useWorkflow,
} from '@/shared/workspace';

const CATEGORIES: readonly { value: IssueStatus; label: string }[] = [
  { value: 'Backlog', label: 'Backlog' },
  { value: 'Todo', label: 'To do' },
  { value: 'InProgress', label: 'In progress' },
  { value: 'InReview', label: 'In review' },
  { value: 'Done', label: 'Done' },
  { value: 'Canceled', label: 'Canceled' },
];

const isClosed = (category: IssueStatus) => category === 'Done' || category === 'Canceled';

/** A transition's rule in the matrix: who may move an issue, and whether it needs an assignee. */
type Rule = 'none' | 'anyone' | 'anyone-assigned' | 'assignee' | 'admins';

const RULES: readonly { value: Rule; label: string }[] = [
  { value: 'none', label: 'Not allowed' },
  { value: 'anyone', label: 'Anyone' },
  { value: 'anyone-assigned', label: 'Anyone, once assigned' },
  { value: 'assignee', label: 'Assignee or admins' },
  { value: 'admins', label: 'Admins only' },
];

const ruleOf = (requireAssignee: boolean, allowedBy: TransitionActor): Rule =>
  allowedBy === 'Admins'
    ? 'admins'
    : allowedBy === 'AssigneeOrAdmins'
      ? 'assignee'
      : requireAssignee
        ? 'anyone-assigned'
        : 'anyone';

const fromRule = (
  rule: Exclude<Rule, 'none'>,
): { requireAssignee: boolean; allowedBy: TransitionActor } => ({
  requireAssignee: rule === 'anyone-assigned',
  allowedBy: rule === 'admins' ? 'Admins' : rule === 'assignee' ? 'AssigneeOrAdmins' : 'Anyone',
});

interface DraftStatus {
  /** Stable while editing: the status id, or a temporary key for new statuses. */
  key: string;
  id: string | null;
  name: string;
  category: IssueStatus;
  wipLimit: string;
  isDefault: boolean;
}

interface Draft {
  statuses: DraftStatus[];
  restrict: boolean;
  /** "fromKey>toKey" to rule. */
  rules: Record<string, Exclude<Rule, 'none'>>;
  /** Removed status id to the key of the status its issues go to. */
  replacements: Record<string, string>;
  removed: { id: string; name: string }[];
}

function draftOf(workflow: WorkflowResponse): Draft {
  return {
    statuses: workflow.statuses.map((status) => ({
      key: status.id,
      id: status.id,
      name: status.name,
      category: status.category,
      wipLimit: status.wipLimit == null ? '' : String(status.wipLimit),
      isDefault: status.isDefault,
    })),
    restrict: workflow.restrictTransitions,
    rules: Object.fromEntries(
      workflow.transitions.map((transition) => [
        `${transition.fromStatusId}>${transition.toStatusId}`,
        ruleOf(transition.requireAssignee, transition.allowedBy),
      ]),
    ) as Draft['rules'],
    replacements: {},
    removed: [],
  };
}

let newKeys = 0;

/**
 * Edits a project's workflow: the statuses (the board's columns) in order, each with a category,
 * an optional WIP limit and one default, and optionally which moves between them are allowed and
 * by whom. Nothing changes until Save; issues of removed statuses move where the admin says.
 */
export function WorkflowEditor({ project }: { project: ProjectResponse }) {
  const { data: workflow } = useWorkflow(project.id);
  if (!workflow) return null;
  // Remount on a new version of the workflow, so the draft starts from it.
  return (
    <Editor
      key={workflow.statuses.map((status) => status.id + status.name).join()}
      project={project}
      workflow={workflow}
    />
  );
}

function Editor({ project, workflow }: { project: ProjectResponse; workflow: WorkflowResponse }) {
  const { organization } = useCurrentOrganization();
  const queryClient = useQueryClient();
  const refreshIssues = useRefreshIssues();
  const update = useUpdateWorkflow();
  const [draft, setDraft] = useState(() => draftOf(workflow));
  const [failure, setFailure] = useState<string | null>(null);
  const readOnly = project.archivedAt != null;

  const change = (next: Partial<Draft>) => {
    setDraft((current) => ({ ...current, ...next }));
  };
  const changeStatus = (key: string, next: Partial<DraftStatus>) => {
    change({
      statuses: draft.statuses.map((status) =>
        status.key === key
          ? { ...status, ...next }
          : next.isDefault
            ? { ...status, isDefault: false }
            : status,
      ),
    });
  };

  const moveStatus = (index: number, offset: -1 | 1) => {
    const statuses = [...draft.statuses];
    const [status] = statuses.splice(index, 1);
    if (!status) return;
    statuses.splice(index + offset, 0, status);
    change({ statuses });
  };

  const addStatus = () => {
    newKeys += 1;
    change({
      statuses: [
        ...draft.statuses,
        {
          key: `new-${String(newKeys)}`,
          id: null,
          name: '',
          category: 'InProgress',
          wipLimit: '',
          isDefault: false,
        },
      ],
    });
  };

  const removeStatus = (status: DraftStatus) => {
    let statuses = draft.statuses.filter((candidate) => candidate.key !== status.key);
    if (status.isDefault) {
      // Another open status becomes the default, so the workflow stays valid.
      const fallback = statuses.find((candidate) => !isClosed(candidate.category)) ?? statuses[0];
      statuses = statuses.map((candidate) =>
        candidate.key === fallback?.key ? { ...candidate, isDefault: true } : candidate,
      );
    }

    const replacement =
      statuses.find((candidate) => candidate.category === status.category) ??
      statuses.find((candidate) => candidate.isDefault) ??
      statuses[0];
    change({
      statuses,
      ...(status.id
        ? {
            removed: [...draft.removed, { id: status.id, name: status.name }],
            replacements: replacement
              ? { ...draft.replacements, [status.id]: replacement.key }
              : draft.replacements,
          }
        : {}),
    });
  };

  const save = async () => {
    if (!organization) return;
    setFailure(null);
    const index = new Map(draft.statuses.map((status, position) => [status.key, position]));
    const transitions = Object.entries(draft.rules).flatMap(([pair, rule]) => {
      const [from = '', to = ''] = pair.split('>');
      const fromIndex = index.get(from);
      const toIndex = index.get(to);
      return fromIndex === undefined || toIndex === undefined
        ? []
        : [{ from: fromIndex, to: toIndex, ...fromRule(rule) }];
    });

    try {
      await update.mutateAsync({
        organizationId: organization.id,
        projectId: project.id,
        data: {
          statuses: draft.statuses.map((status) => ({
            id: status.id,
            name: status.name.trim(),
            category: status.category,
            wipLimit: status.wipLimit ? Number(status.wipLimit) : null,
            isDefault: status.isDefault,
          })),
          restrictTransitions: draft.restrict,
          transitions,
          replacements: draft.removed.flatMap((removed) => {
            const target = index.get(draft.replacements[removed.id] ?? '');
            return target === undefined
              ? []
              : [{ removedStatusId: removed.id, replacementIndex: target }];
          }),
        },
      });
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: getGetWorkflowQueryKey(organization.id, project.id),
        }),
        queryClient.invalidateQueries({ predicate: (query) => isBoardQuery(query.queryKey) }),
        refreshIssues(),
      ]);
      toast.success('Workflow saved');
    } catch (error) {
      setFailure(errorMessage(error));
    }
  };

  const statuses = draft.statuses;
  const valid =
    statuses.length > 0 &&
    statuses.every((status) => status.name.trim().length > 0) &&
    statuses.some((status) => status.isDefault && !isClosed(status.category));

  return (
    <Card>
      <CardHeader>
        <CardTitle>Workflow</CardTitle>
        <CardDescription>
          Statuses are the board&apos;s columns, in this order. Each counts as one category in
          filters and reports. New issues start in the default status.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-5">
        {failure && <Alert variant="destructive">{failure}</Alert>}

        <ol className="flex flex-col gap-2" aria-label="Statuses">
          {statuses.map((status, position) => {
            const label = status.name || `status ${String(position + 1)}`;
            return (
              <li
                key={status.key}
                className="flex flex-wrap items-center gap-2 rounded-lg border p-2"
              >
                <div className="flex">
                  <Button
                    variant="ghost"
                    size="icon-sm"
                    aria-label={`Move ${label} up`}
                    disabled={readOnly || position === 0}
                    onClick={() => {
                      moveStatus(position, -1);
                    }}
                  >
                    <ArrowUp />
                  </Button>
                  <Button
                    variant="ghost"
                    size="icon-sm"
                    aria-label={`Move ${label} down`}
                    disabled={readOnly || position === statuses.length - 1}
                    onClick={() => {
                      moveStatus(position, 1);
                    }}
                  >
                    <ArrowDown />
                  </Button>
                </div>
                <StatusIcon category={CATEGORY_GLYPHS[status.category]} />
                <Input
                  aria-label={`Name of ${label}`}
                  value={status.name}
                  maxLength={40}
                  placeholder="Status name"
                  disabled={readOnly}
                  className="h-8 w-40 flex-1"
                  onChange={(event) => {
                    changeStatus(status.key, { name: event.target.value });
                  }}
                />
                <Select
                  aria-label={`Category of ${label}`}
                  value={status.category}
                  disabled={readOnly}
                  onChange={(event) => {
                    changeStatus(status.key, { category: event.target.value as IssueStatus });
                  }}
                >
                  {CATEGORIES.map((category) => (
                    <option key={category.value} value={category.value}>
                      {category.label}
                    </option>
                  ))}
                </Select>
                <Input
                  aria-label={`WIP limit of ${label}`}
                  inputMode="numeric"
                  placeholder="No limit"
                  value={status.wipLimit}
                  disabled={readOnly}
                  className="h-8 w-24"
                  onChange={(event) => {
                    changeStatus(status.key, {
                      wipLimit: event.target.value.replace(/\D/g, '').slice(0, 3),
                    });
                  }}
                />
                <label className="flex items-center gap-1.5 text-[13px] text-muted-foreground">
                  <input
                    type="radio"
                    name="default-status"
                    checked={status.isDefault}
                    disabled={readOnly || isClosed(status.category)}
                    onChange={() => {
                      changeStatus(status.key, { isDefault: true });
                    }}
                  />
                  Default
                </label>
                <Button
                  variant="ghost"
                  size="icon-sm"
                  aria-label={`Remove ${label}`}
                  disabled={readOnly || statuses.length === 1}
                  onClick={() => {
                    removeStatus(status);
                  }}
                >
                  <Trash2 />
                </Button>
              </li>
            );
          })}
        </ol>

        {!readOnly && (
          <Button variant="secondary" size="sm" className="self-start" onClick={addStatus}>
            <Plus />
            Add status
          </Button>
        )}

        {draft.removed.length > 0 && (
          <div className="flex flex-col gap-2 rounded-lg bg-warning-soft p-3">
            <p className="text-sm font-medium">Where should their issues go?</p>
            {draft.removed.map((removed) => (
              <label key={removed.id} className="flex flex-wrap items-center gap-2 text-sm">
                Issues in {removed.name} move to
                <Select
                  aria-label={`Where issues in ${removed.name} go`}
                  value={draft.replacements[removed.id] ?? ''}
                  onChange={(event) => {
                    change({
                      replacements: { ...draft.replacements, [removed.id]: event.target.value },
                    });
                  }}
                >
                  {statuses.map((status) => (
                    <option key={status.key} value={status.key}>
                      {status.name || 'New status'}
                    </option>
                  ))}
                </Select>
              </label>
            ))}
          </div>
        )}

        <div className="flex items-start gap-3 border-t pt-4">
          <Switch
            id="restrict-transitions"
            checked={draft.restrict}
            disabled={readOnly}
            onCheckedChange={(checked) => {
              change({ restrict: checked });
            }}
          />
          <label htmlFor="restrict-transitions" className="text-sm">
            <span className="font-medium">Restrict transitions</span>
            <span className="block text-muted-foreground">
              Only the moves allowed below are possible, by the people they name. Reordering within
              a status is always allowed.
            </span>
          </label>
        </div>

        {draft.restrict && (
          <div className="overflow-x-auto">
            <table className="text-[13px]">
              <caption className="sr-only">
                Allowed transitions: from the row status to the column status
              </caption>
              <thead>
                <tr>
                  <th scope="col" className="px-2 py-1 text-left font-medium text-muted-foreground">
                    From \ To
                  </th>
                  {statuses.map((to) => (
                    <th
                      key={to.key}
                      scope="col"
                      className="px-2 py-1 text-left font-medium whitespace-nowrap"
                    >
                      {to.name || 'New status'}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {statuses.map((from) => (
                  <tr key={from.key}>
                    <th scope="row" className="px-2 py-1 text-left font-medium whitespace-nowrap">
                      {from.name || 'New status'}
                    </th>
                    {statuses.map((to) => (
                      <td key={to.key} className="px-1 py-1">
                        {from.key === to.key ? (
                          <span className="block px-2 text-subtle-foreground" aria-hidden="true">
                            ·
                          </span>
                        ) : (
                          <Select
                            aria-label={`From ${from.name} to ${to.name}`}
                            value={draft.rules[`${from.key}>${to.key}`] ?? 'none'}
                            disabled={readOnly}
                            className="h-7 text-xs"
                            onChange={(event) => {
                              const pair = `${from.key}>${to.key}`;
                              const rule = event.target.value as Rule;
                              const rules = Object.fromEntries(
                                Object.entries(draft.rules).filter(([key]) => key !== pair),
                              ) as Draft['rules'];
                              change({
                                rules: rule === 'none' ? rules : { ...rules, [pair]: rule },
                              });
                            }}
                          >
                            {RULES.map((rule) => (
                              <option key={rule.value} value={rule.value}>
                                {rule.label}
                              </option>
                            ))}
                          </Select>
                        )}
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {!readOnly && (
          <div className="flex gap-2">
            <Button disabled={!valid || update.isPending} onClick={() => void save()}>
              Save workflow
            </Button>
            <Button
              variant="ghost"
              onClick={() => {
                setDraft(draftOf(workflow));
                setFailure(null);
              }}
            >
              Discard changes
            </Button>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
