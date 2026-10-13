import { act, renderHook } from '@testing-library/react';
import type { ReactNode } from 'react';
import { createMemoryRouter, RouterProvider, useLocation } from 'react-router';
import { describe, expect, it } from 'vitest';
import { toListParams, useIssueFilters, type IssueFilters } from './filters';

function renderFilters(url: string) {
  let search = '';
  const wrapper = ({ children }: { children: ReactNode }) => {
    function Probe() {
      search = useLocation().search;
      return children;
    }
    const router = createMemoryRouter([{ path: '*', Component: Probe }], {
      initialEntries: [url],
    });
    return <RouterProvider router={router} />;
  };
  const hook = renderHook(() => useIssueFilters(), { wrapper });
  return { ...hook, search: () => search };
}

const none: IssueFilters = {
  status: [],
  priority: [],
  type: [],
  assignee: [],
  label: [],
  q: '',
  sort: 'Updated',
};

describe('useIssueFilters', () => {
  it('reads filters from the URL and ignores unknown values', () => {
    const { result } = renderFilters(
      '/?status=Todo&status=Bogus&priority=High&sort=Priority&q=crash',
    );

    expect(result.current.filters).toEqual({
      ...none,
      status: ['Todo'],
      priority: ['High'],
      q: 'crash',
      sort: 'Priority',
    });
    expect(result.current.active).toBe(2);
  });

  it('writes changes to the URL and leaves the default sort out', () => {
    const { result, search } = renderFilters('/?sort=Priority');

    act(() => {
      result.current.update({ status: ['Todo', 'InProgress'], sort: 'Updated' });
    });

    expect(search()).toBe('?status=Todo&status=InProgress');
  });
});

describe('toListParams', () => {
  it('sends only the filters that are set', () => {
    expect(toListParams(none)).toEqual({ sort: 'Updated', limit: 50 });
  });

  it('scopes to a project and splits "unassigned" from assignee ids', () => {
    expect(toListParams({ ...none, assignee: ['u1', 'none'], label: ['l1'] }, 'p1')).toEqual({
      projectId: ['p1'],
      assigneeId: ['u1'],
      unassigned: true,
      labelId: ['l1'],
      sort: 'Updated',
      limit: 50,
    });
  });
});
