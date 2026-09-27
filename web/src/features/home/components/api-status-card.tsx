import { useGetSystemInfo } from '@/shared/api/generated/endpoints';
import { Badge } from '@/shared/ui/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/shared/ui/card';
import { Skeleton } from '@/shared/ui/skeleton';

/** Shows whether the frontend can reach the API, and which build and environment it is talking to. */
export function ApiStatusCard() {
  const { data, isPending, isError, error } = useGetSystemInfo();

  return (
    <Card className="max-w-md">
      <CardHeader>
        <div className="flex items-center justify-between gap-4">
          <CardTitle>API status</CardTitle>
          {isPending ? (
            <Skeleton className="h-5 w-20 rounded-full" />
          ) : isError ? (
            <Badge variant="destructive">Unreachable</Badge>
          ) : (
            <Badge variant="success">
              <span className="size-1.5 rounded-full bg-success" aria-hidden="true" />
              Connected
            </Badge>
          )}
        </div>
        <CardDescription>The backend this app is connected to.</CardDescription>
      </CardHeader>

      <CardContent>
        {isPending ? (
          <div className="flex flex-col gap-2" aria-busy="true" aria-label="Loading API status">
            <Skeleton className="h-4 w-40" />
            <Skeleton className="h-4 w-32" />
          </div>
        ) : isError ? (
          <p className="text-sm text-destructive">{error.message}</p>
        ) : (
          <dl className="grid grid-cols-[auto_1fr] gap-x-6 gap-y-1 text-sm">
            <dt className="text-muted-foreground">Version</dt>
            <dd className="font-mono">{data.version}</dd>
            <dt className="text-muted-foreground">Environment</dt>
            <dd>{data.environment}</dd>
          </dl>
        )}
      </CardContent>
    </Card>
  );
}
