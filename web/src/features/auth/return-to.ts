/**
 * Where to go after signing in. Only same-app paths are allowed: an absolute or protocol-relative
 * URL in `returnTo` would make the sign-in page an open redirect.
 */
export function safeReturnTo(returnTo: string | null): string {
  if (!returnTo?.startsWith('/') || returnTo.startsWith('//') || returnTo.startsWith('/\\')) {
    return '/';
  }
  return returnTo;
}
