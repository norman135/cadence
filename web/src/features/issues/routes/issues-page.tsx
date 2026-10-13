import { Plus, Settings } from 'lucide-react';
import { lazy, Suspense, useState } from 'react';
import { Link, useParams } from 'react-router';
import { Button } from '@/shared/ui/button';
import { useCurrentOrganization, useCurrentProject } from '@/shared/workspace';
import { FilterBar } from '../components/filter-bar';
import { useColumns } from '../components/issue-columns';
import { IssueDrawer } from '../components/issue-drawer';
import { IssueList } from '../components/issue-list';
import { toListParams, useIssueFilters } from '../lib/filters';

const CreateIssueDialog = lazy(() => import('../dialogs/create-issue-dialog'));

/**
 * The issue list for one project (`/:org/projects/:key`) or across every project the user can see
 * (`/:org/issues`). Filters, sort and the open issue live in the URL.
 */
export function IssuesPage() {
  const { organization } = useCurrentOrganization();
  const { project, isPending } = useCurrentProject();
  const scoped = !!useParams().projectKey;
  const { filters, update, active } = useIssueFilters();
  const [columns, setColumns] = useColumns();
  const [creating, setCreating] = useState(false);

  if (scoped && isPending) return null;
  if (scoped && !project) {
    return (
      <div className="py-16 text-center">
        <p className="font-semibold">Project not found</p>
        <p className="mt-1 text-sm text-muted-foreground">
          It doesn&apos;t exist, or you don&apos;t have access to it.
        </p>
      </div>
    );
  }

  const canCreate = !project || (project.role !== 'Viewer' && project.archivedAt == null);

  return (
    <div className="mx-auto flex max-w-6xl flex-col gap-4">
      <div className="flex flex-wrap items-center gap-3">
        <div className="min-w-0">
          <h1 className="text-2xl font-semibold tracking-[-0.02em]">
            {project ? project.name : 'All issues'}
          </h1>
          <p className="text-sm text-muted-foreground">
            {project
              ? `${String(project.openIssueCount)} open · ${project.key}${project.archivedAt ? ' · archived' : ''}`
              : 'Every issue in the projects you can see.'}
          </p>
        </div>
        <span className="flex-1" />
        {project?.role === 'Admin' && (
          <Button variant="ghost" size="sm" asChild>
            <Link to={`/${organization?.slug ?? ''}/projects/${project.key}/settings`}>
              <Settings />
              Settings
            </Link>
          </Button>
        )}
        {canCreate && (
          <Button
            size="sm"
            onClick={() => {
              setCreating(true);
            }}
          >
            <Plus />
            New issue
          </Button>
        )}
      </div>

      <FilterBar
        filters={filters}
        update={update}
        active={active}
        projectId={project?.id}
        columns={columns}
        onColumnsChange={setColumns}
      />

      <IssueList
        params={toListParams(filters, project?.id)}
        columns={columns}
        empty={
          active > 0 || filters.q ? (
            <>
              <p className="font-semibold">No issues match</p>
              <p className="text-sm text-muted-foreground">Try fewer filters or other words.</p>
            </>
          ) : (
            <>
              <p className="font-semibold">No issues yet</p>
              <p className="text-sm text-muted-foreground">
                Create the first one to start planning the work.
              </p>
            </>
          )
        }
      />

      <IssueDrawer />
      {creating && (
        <Suspense fallback={null}>
          <CreateIssueDialog open projectId={project?.id} onOpenChange={setCreating} />
        </Suspense>
      )}
    </div>
  );
}
