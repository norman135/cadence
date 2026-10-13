import { useQueryClient } from '@tanstack/react-query';
import { useParams } from 'react-router';
import {
  getGetIssueQueryKey,
  getListIssuesInfiniteQueryKey,
  getListProjectsQueryKey,
  useGetWorkflow,
  useListLabels,
  useListProjectMembers,
  useListProjects,
} from '@/shared/api/generated/endpoints';
import type { ProjectResponse } from '@/shared/api/generated/model';
import { useCurrentOrganization } from './current';

/** The projects the user can see in the current organization (shared by navigation and pages). */
export function useProjects() {
  const { organization } = useCurrentOrganization();
  return useListProjects(organization?.id ?? '', undefined, {
    query: { enabled: !!organization, staleTime: 30_000 },
  });
}

/**
 * The project named by the `:projectKey` route parameter. Routes use keys for readable URLs
 * (`/acme/projects/ATL`); API calls use ids.
 */
export function useCurrentProject(): { project: ProjectResponse | undefined; isPending: boolean } {
  const { projectKey } = useParams();
  const { data, isPending } = useProjects();
  return {
    project: data?.find((project) => project.key === projectKey?.toUpperCase()),
    isPending,
  };
}

/** The organization's labels. */
export function useLabels() {
  const { organization } = useCurrentOrganization();
  return useListLabels(organization?.id ?? '', {
    query: { enabled: !!organization, staleTime: 60_000 },
  });
}

/** Everyone with access to a project: the people issues can be assigned to. */
export function useProjectPeople(projectId: string | undefined) {
  const { organization } = useCurrentOrganization();
  return useListProjectMembers(organization?.id ?? '', projectId ?? '', {
    query: { enabled: !!organization && !!projectId, staleTime: 60_000 },
  });
}

/** A project's workflow: its statuses in board order, and which moves are allowed. Rarely changes. */
export function useWorkflow(projectId: string | undefined) {
  const { organization } = useCurrentOrganization();
  return useGetWorkflow(organization?.id ?? '', projectId ?? '', {
    query: { enabled: !!organization && !!projectId, staleTime: 5 * 60_000 },
  });
}

/** Board queries are keyed by their URL, which ends in /board, plus the quick filters. */
export const isBoardQuery = (queryKey: readonly unknown[]) =>
  typeof queryKey[0] === 'string' && queryKey[0].endsWith('/board');

/**
 * After an issue changes: refetch issue lists, boards, the issue itself and project counts. Lists share a
 * key prefix, so one call covers every filter combination.
 */
export function useRefreshIssues() {
  const queryClient = useQueryClient();
  const { organization } = useCurrentOrganization();

  return async (issueKey?: string) => {
    if (!organization) return;
    const [, listPath] = getListIssuesInfiniteQueryKey(organization.id);
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['infinite', listPath] }),
      queryClient.invalidateQueries({ queryKey: getListProjectsQueryKey(organization.id) }),
      queryClient.invalidateQueries({ predicate: (query) => isBoardQuery(query.queryKey) }),
      issueKey
        ? queryClient.invalidateQueries({
            queryKey: getGetIssueQueryKey(organization.id, issueKey),
          })
        : Promise.resolve(),
    ]);
  };
}
