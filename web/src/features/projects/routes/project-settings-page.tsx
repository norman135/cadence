import { useQueryClient } from '@tanstack/react-query';
import { Archive, ArchiveRestore, Trash2 } from 'lucide-react';
import { useState } from 'react';
import { toast } from 'sonner';
import {
  getListProjectMembersQueryKey,
  getListProjectsQueryKey,
  useAddProjectMember,
  useArchiveProject,
  useChangeProjectMemberRole,
  useListMembers,
  useRemoveProjectMember,
  useRestoreProject,
  useUpdateProject,
} from '@/shared/api/generated/endpoints';
import type { ProjectResponse, ProjectRole } from '@/shared/api/generated/model';
import { errorMessage } from '@/shared/api/api-error';
import { Avatar } from '@/shared/ui/avatar';
import { Badge } from '@/shared/ui/badge';
import { Button } from '@/shared/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/card';
import { Select } from '@/shared/ui/select';
import { TextField } from '@/shared/ui/text-field';
import { useCurrentOrganization, useCurrentProject, useProjectPeople } from '@/shared/workspace';

const ROLES: readonly ProjectRole[] = ['Admin', 'Member', 'Viewer'];

/** A project's settings: details, who has access, and archiving. Project admins only. */
export function ProjectSettingsPage() {
  const { project, isPending } = useCurrentProject();
  if (isPending) return null;
  if (project?.role !== 'Admin') {
    return (
      <div className="py-16 text-center">
        <p className="font-semibold">Project settings unavailable</p>
        <p className="mt-1 text-sm text-muted-foreground">
          Only project admins can change a project.
        </p>
      </div>
    );
  }

  return (
    <div className="mx-auto flex max-w-2xl flex-col gap-6">
      <h1 className="text-2xl font-semibold tracking-[-0.02em]">{project.name} settings</h1>
      <DetailsCard key={project.id} project={project} />
      <MembersCard project={project} />
      <ArchiveCard project={project} />
    </div>
  );
}

function useRefreshProjects() {
  const queryClient = useQueryClient();
  const { organization } = useCurrentOrganization();
  return (projectId?: string) =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: getListProjectsQueryKey(organization?.id ?? '') }),
      projectId
        ? queryClient.invalidateQueries({
            queryKey: getListProjectMembersQueryKey(organization?.id ?? '', projectId),
          })
        : Promise.resolve(),
    ]);
}

function DetailsCard({ project }: { project: ProjectResponse }) {
  const { organization } = useCurrentOrganization();
  const update = useUpdateProject();
  const refresh = useRefreshProjects();
  const [name, setName] = useState(project.name);
  const [description, setDescription] = useState(project.description ?? '');
  const dirty = name !== project.name || description !== (project.description ?? '');

  const save = async () => {
    try {
      await update.mutateAsync({
        organizationId: organization?.id ?? '',
        projectId: project.id,
        data: { name, description },
      });
      await refresh();
      toast.success('Project saved');
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>Details</CardTitle>
        <CardDescription>
          The key <span className="font-mono">{project.key}</span> starts every issue key and
          can&apos;t change.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <TextField
          label="Name"
          value={name}
          maxLength={80}
          onChange={(event) => {
            setName(event.target.value);
          }}
        />
        <TextField
          label="Description"
          value={description}
          maxLength={2000}
          onChange={(event) => {
            setDescription(event.target.value);
          }}
        />
        <Button
          className="self-start"
          disabled={!dirty || name.trim().length < 2 || update.isPending}
          onClick={() => void save()}
        >
          Save
        </Button>
      </CardContent>
    </Card>
  );
}

function MembersCard({ project }: { project: ProjectResponse }) {
  const { organization } = useCurrentOrganization();
  const organizationId = organization?.id ?? '';
  const { data: people } = useProjectPeople(project.id);
  const { data: members } = useListMembers(
    organizationId,
    { limit: 100 },
    { query: { enabled: !!organization } },
  );
  const add = useAddProjectMember();
  const changeRole = useChangeProjectMemberRole();
  const remove = useRemoveProjectMember();
  const refresh = useRefreshProjects();
  const [candidate, setCandidate] = useState('');
  const [role, setRole] = useState<ProjectRole>('Member');

  const withAccess = new Set(people?.map((person) => person.userId));
  const candidates = members?.items.filter((member) => !withAccess.has(member.userId)) ?? [];

  const run = async (action: () => Promise<unknown>) => {
    try {
      await action();
      await refresh(project.id);
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>People</CardTitle>
        <CardDescription>
          Members see the project. Organization owners and admins always have access.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <ul className="divide-y rounded-lg border">
          {people?.map((person) => (
            <li key={person.userId} className="flex items-center gap-3 px-3 py-2.5">
              <Avatar name={person.displayName} />
              <div className="min-w-0 flex-1 leading-tight">
                <p className="truncate text-sm font-medium">{person.displayName}</p>
                <p className="truncate text-xs text-muted-foreground">{person.email}</p>
              </div>
              {person.isImplicit ? (
                <Badge>Organization admin</Badge>
              ) : (
                <>
                  <Select
                    aria-label={`Role of ${person.displayName}`}
                    value={person.role}
                    onChange={(event) =>
                      void run(() =>
                        changeRole.mutateAsync({
                          organizationId,
                          projectId: project.id,
                          userId: person.userId,
                          data: { role: event.target.value as ProjectRole },
                        }),
                      )
                    }
                  >
                    {ROLES.map((option) => (
                      <option key={option} value={option}>
                        {option}
                      </option>
                    ))}
                  </Select>
                  <Button
                    variant="ghost"
                    size="icon"
                    aria-label={`Remove ${person.displayName}`}
                    onClick={() =>
                      void run(() =>
                        remove.mutateAsync({
                          organizationId,
                          projectId: project.id,
                          userId: person.userId,
                        }),
                      )
                    }
                  >
                    <Trash2 />
                  </Button>
                </>
              )}
            </li>
          ))}
        </ul>

        {candidates.length > 0 && (
          <form
            className="flex flex-wrap items-center gap-2"
            onSubmit={(event) => {
              event.preventDefault();
              if (!candidate) return;
              void run(async () => {
                await add.mutateAsync({
                  organizationId,
                  projectId: project.id,
                  data: { userId: candidate, role },
                });
                setCandidate('');
              });
            }}
          >
            <Select
              aria-label="Person to add"
              value={candidate}
              onChange={(event) => {
                setCandidate(event.target.value);
              }}
              className="min-w-48 flex-1"
            >
              <option value="">Add someone from the organization…</option>
              {candidates.map((member) => (
                <option key={member.userId} value={member.userId}>
                  {member.displayName}
                </option>
              ))}
            </Select>
            <Select
              aria-label="Role"
              value={role}
              onChange={(event) => {
                setRole(event.target.value as ProjectRole);
              }}
            >
              {ROLES.map((option) => (
                <option key={option} value={option}>
                  {option}
                </option>
              ))}
            </Select>
            <Button type="submit" variant="secondary" disabled={!candidate || add.isPending}>
              Add
            </Button>
          </form>
        )}
      </CardContent>
    </Card>
  );
}

function ArchiveCard({ project }: { project: ProjectResponse }) {
  const { organization } = useCurrentOrganization();
  const archive = useArchiveProject();
  const restore = useRestoreProject();
  const refresh = useRefreshProjects();
  const archived = project.archivedAt != null;

  const toggle = async () => {
    try {
      const variables = { organizationId: organization?.id ?? '', projectId: project.id };
      await (archived ? restore.mutateAsync(variables) : archive.mutateAsync(variables));
      await refresh();
      toast.success(archived ? 'Project restored' : 'Project archived');
    } catch (error) {
      toast.error(errorMessage(error));
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>{archived ? 'Restore project' : 'Archive project'}</CardTitle>
        <CardDescription>
          {archived
            ? 'Restoring brings the project back to navigation and allows changes again.'
            : 'Archived projects leave navigation and become read-only. Nothing is deleted.'}
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Button variant="secondary" onClick={() => void toggle()}>
          {archived ? <ArchiveRestore /> : <Archive />}
          {archived ? 'Restore project' : 'Archive project'}
        </Button>
      </CardContent>
    </Card>
  );
}
