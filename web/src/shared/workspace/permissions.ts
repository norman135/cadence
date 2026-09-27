import type { OrganizationRole } from '@/shared/api/generated/model';

/**
 * Mirrors the server's role → permission matrix (Domain/Organizations/Permissions.cs) so the UI
 * only offers actions the user can perform. The server remains the authority.
 */
export type Permission =
  | 'organization.update'
  | 'organization.delete'
  | 'members.read'
  | 'members.invite'
  | 'members.manage';

const byRole: Record<OrganizationRole, readonly Permission[]> = {
  Guest: [],
  Member: ['members.read'],
  Admin: ['organization.update', 'members.read', 'members.invite', 'members.manage'],
  Owner: [
    'organization.update',
    'organization.delete',
    'members.read',
    'members.invite',
    'members.manage',
  ],
};

export const can = (role: OrganizationRole, permission: Permission) =>
  byRole[role].includes(permission);

export const ROLES: readonly OrganizationRole[] = ['Owner', 'Admin', 'Member', 'Guest'];
