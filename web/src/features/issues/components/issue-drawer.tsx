import { Maximize2, X } from 'lucide-react';
import { lazy, Suspense } from 'react';
import { Link, useSearchParams } from 'react-router';
import { Button } from '@/shared/ui/button';
import { Dialog, DialogClose, DialogContent, DialogTitle } from '@/shared/ui/dialog';
import { Skeleton } from '@/shared/ui/skeleton';
import { useCurrentOrganization } from '@/shared/workspace';

// Lists load without the issue view; it is fetched the first time someone opens an issue.
const IssueDetail = lazy(() =>
  import('./issue-detail').then((module) => ({ default: module.IssueDetail })),
);

/**
 * The issue named by `?issue=ATL-142`, in a panel over the list, so people can work through a list
 * without losing their place. The URL makes it linkable; Escape or the back button closes it.
 */
export function IssueDrawer() {
  const [params, setParams] = useSearchParams();
  const { organization } = useCurrentOrganization();
  const key = params.get('issue');

  const close = () => {
    setParams((current) => {
      const next = new URLSearchParams(current);
      next.delete('issue');
      return next;
    });
  };

  return (
    <Dialog
      open={!!key}
      onOpenChange={(open) => {
        if (!open) close();
      }}
    >
      <DialogContent
        aria-describedby={undefined}
        className="top-0 right-0 left-auto h-svh w-full max-w-[720px] translate-x-0 overflow-y-auto rounded-none rounded-l-xl border-y-0 border-r-0 p-0"
      >
        {key && (
          <>
            <div className="sticky top-0 z-10 flex h-12 items-center gap-1 border-b bg-popover/95 px-4 backdrop-blur">
              <DialogTitle className="font-mono text-sm font-normal text-muted-foreground">
                {key.toUpperCase()}
              </DialogTitle>
              <span className="flex-1" />
              <Button variant="ghost" size="icon-sm" asChild aria-label="Open full page">
                <Link to={`/${organization?.slug ?? ''}/issues/${key}`}>
                  <Maximize2 />
                </Link>
              </Button>
              <DialogClose asChild>
                <Button variant="ghost" size="icon-sm" aria-label="Close">
                  <X />
                </Button>
              </DialogClose>
            </div>
            <div className="p-6">
              <Suspense fallback={<Skeleton className="h-8 w-2/3" />}>
                <IssueDetail issueKey={key} inDrawer />
              </Suspense>
            </div>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}
