import { useQueryClient } from '@tanstack/react-query';
import { useParams } from 'react-router';
import {
  getGetIssueQueryKey,
  getListIssuesInfiniteQueryKey,
  getListProjectsQueryKey,
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

/**
 * After an issue changes: refetch issue lists, the issue itself and project counts. Lists share a
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
      issueKey
        ? queryClient.invalidateQueries({
            queryKey: getGetIssueQueryKey(organization.id, issueKey),
          })
        : Promise.resolve(),
    ]);
  };
}
