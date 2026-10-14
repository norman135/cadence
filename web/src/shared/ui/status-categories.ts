import type { IssueStatus } from '@/shared/api/generated/model';
import type { WorkflowCategory } from './work-glyphs';

/** The glyph of each status category (the API's names), for any status of any workflow. */
export const CATEGORY_GLYPHS: Record<IssueStatus, WorkflowCategory> = {
  Backlog: 'backlog',
  Todo: 'todo',
  InProgress: 'progress',
  InReview: 'review',
  Done: 'done',
  Canceled: 'canceled',
};
