import { useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import { getListProjectsQueryKey, useCreateProject } from '@/shared/api/generated/endpoints';
import { errorMessage } from '@/shared/api/api-error';
import { Alert } from '@/shared/ui/alert';
import { Button } from '@/shared/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogTitle } from '@/shared/ui/dialog';
import { Label } from '@/shared/ui/label';
import { TextField } from '@/shared/ui/text-field';
import { useCurrentOrganization } from '@/shared/workspace';
import { suggestKey } from '../lib/suggest-key';

const KEY_PATTERN = /^[A-Z][A-Z0-9]{1,9}$/;

/** Creates a project; the caller becomes its admin. The key is suggested from the name until edited. */
export default function CreateProjectDialog({
  open,
  onOpenChange,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const { organization } = useCurrentOrganization();
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const create = useCreateProject();
  const [name, setName] = useState('');
  const [key, setKey] = useState<string | null>(null);
  const [description, setDescription] = useState('');
  const [failure, setFailure] = useState<string | null>(null);

  const effectiveKey = key ?? (name.trim() ? suggestKey(name) : '');
  const keyValid = KEY_PATTERN.test(effectiveKey);

  const submit = async () => {
    if (!organization) return;
    setFailure(null);
    try {
      const project = await create.mutateAsync({
        organizationId: organization.id,
        data: { name, key: key === null ? null : effectiveKey, description: description || null },
      });
      await queryClient.invalidateQueries({ queryKey: getListProjectsQueryKey(organization.id) });
      onOpenChange(false);
      await navigate(`/${organization.slug}/projects/${project.key}`);
    } catch (error) {
      setFailure(errorMessage(error));
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogTitle>New project</DialogTitle>
        <DialogDescription>
          A project holds issues. Its key starts every issue key, like {effectiveKey || 'ATL'}-1.
        </DialogDescription>
        <form
          className="mt-4 flex flex-col gap-4"
          onSubmit={(event) => {
            event.preventDefault();
            void submit();
          }}
        >
          {failure && <Alert variant="destructive">{failure}</Alert>}
          <TextField
            label="Name"
            autoFocus
            placeholder="Atlas Mobile"
            value={name}
            maxLength={80}
            onChange={(event) => {
              setName(event.target.value);
            }}
          />
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="project-key">Key</Label>
            <input
              id="project-key"
              value={effectiveKey}
              maxLength={10}
              aria-invalid={effectiveKey !== '' && !keyValid ? true : undefined}
              onChange={(event) => {
                setKey(event.target.value.toUpperCase().replace(/[^A-Z0-9]/g, ''));
              }}
              className="h-8 w-32 rounded-md border border-input bg-card px-2.5 font-mono text-sm uppercase outline-none focus-visible:border-primary focus-visible:ring-[3px] focus-visible:ring-ring aria-invalid:border-destructive"
            />
            <p className="text-xs text-muted-foreground">
              2 to 10 letters or digits, starting with a letter. It can&apos;t be changed later.
            </p>
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="project-description">Description</Label>
            <textarea
              id="project-description"
              rows={3}
              value={description}
              maxLength={2000}
              onChange={(event) => {
                setDescription(event.target.value);
              }}
              className="w-full resize-y rounded-md border border-input bg-card px-2.5 py-1.5 text-sm outline-none focus-visible:border-primary focus-visible:ring-[3px] focus-visible:ring-ring"
            />
          </div>
          <div className="flex justify-end gap-2">
            <Button
              type="button"
              variant="secondary"
              onClick={() => {
                onOpenChange(false);
              }}
            >
              Cancel
            </Button>
            <Button
              type="submit"
              disabled={name.trim().length < 2 || !keyValid || create.isPending}
            >
              Create project
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
