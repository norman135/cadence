import { Kanban, List, Plus, Settings } from 'lucide-react';
import { NavLink, Link } from 'react-router';
import type { ProjectResponse } from '@/shared/api/generated/model';
import { cn } from '@/shared/lib/utils';
import { Button } from '@/shared/ui/button';

const view =
  'flex h-7 items-center gap-1.5 rounded-md px-2.5 text-[13px] font-medium text-muted-foreground outline-none hover:text-foreground focus-visible:outline-2 focus-visible:outline-ring [&_svg]:size-3.5';

/**
 * The top of a project's pages: name, counts, the List and Board views (design board 07), and the
 * project's actions. Without a project it's the header of All issues.
 */
export function ProjectHeader({
  project,
  basePath,
  onNewIssue,
}: {
  project: ProjectResponse | undefined;
  /** The organization's path, like /acme. */
  basePath: string;
  onNewIssue?: (() => void) | undefined;
}) {
  const projectPath = project ? `${basePath}/projects/${project.key}` : undefined;
  return (
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
      {projectPath && (
        <nav
          aria-label="Views"
          className="flex items-center gap-0.5 rounded-lg border bg-sunken p-0.5"
        >
          <NavLink
            to={projectPath}
            end
            className={({ isActive }) => cn(view, isActive && 'bg-card text-foreground shadow-xs')}
          >
            <List aria-hidden="true" />
            List
          </NavLink>
          <NavLink
            to={`${projectPath}/board`}
            className={({ isActive }) => cn(view, isActive && 'bg-card text-foreground shadow-xs')}
          >
            <Kanban aria-hidden="true" />
            Board
          </NavLink>
        </nav>
      )}
      <span className="flex-1" />
      {project?.role === 'Admin' && projectPath && (
        <Button variant="ghost" size="sm" asChild>
          <Link to={`${projectPath}/settings`}>
            <Settings />
            Settings
          </Link>
        </Button>
      )}
      {onNewIssue && (
        <Button size="sm" onClick={onNewIssue}>
          <Plus />
          New issue
        </Button>
      )}
    </div>
  );
}
