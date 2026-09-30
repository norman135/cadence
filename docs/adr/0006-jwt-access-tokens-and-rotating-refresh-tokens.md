# 0006. Short-lived JWT access tokens with rotating refresh tokens

- Status: Accepted
- Date: 2026-09-26

## Context

The React SPA and the API share one origin ([ADR-0004](0004-single-container-serves-the-spa.md)). Sessions must:

- survive page reloads and last days, without asking users to sign in constantly
- keep credentials away from JavaScript, where an XSS bug could read them
- be revocable: sign-out, password changes, and stolen tokens
- avoid a database or cache lookup on every API call, because the host is small

Plain cookie sessions meet most of these but need server-side session state or a lookup per request. Long-lived JWTs in `localStorage` need no lookups but are readable by any injected script and can't be revoked.

## Decision

Two tokens, each with one job:

- **Access token.** A JWT signed with HMAC-SHA256, valid for **10 minutes**, carrying `sub`, `email` and `name`. The SPA keeps it **only in memory** and sends it as `Authorization: Bearer`. The API validates it statelessly: a signature check, with no database or cache lookup.
- **Refresh token.** 32 random bytes in a cookie:
  - `HttpOnly`: scripts cannot read it
  - `SameSite=Strict`: browsers don't send it cross-site, which prevents CSRF
  - `Path=/api/v1/auth`: it is sent only to the auth endpoints
  - `Secure` over HTTPS

  Only its **SHA-256 hash** is stored, so a database leak does not reveal usable tokens.

Every refresh **rotates** the refresh token: the old one is revoked and a new one is issued in the same *family* (the session). If an already-exchanged token is presented again, the token was copied, so the **whole family is revoked** and both the attacker and the victim must sign in again. A 10-second grace period accepts a just-rotated token, so two tabs refreshing at the same moment aren't mistaken for theft. The SPA also serializes refreshes across tabs with the Web Locks API. Concurrent rotations of one token are caught with optimistic concurrency on PostgreSQL's `xmin`.

Password reset and password change end every session. A password change starts a fresh session for the device that made the change.

ASP.NET Core Identity provides accounts and credentials: PBKDF2 password hashing, lockout after 5 failures, security stamps, and data-protected email-confirmation and password-reset tokens. Reset tokens get their own provider with a 2-hour lifetime. Passwords only need to be at least 10 characters, with no composition rules (NIST SP 800-63B). Sign-in failures for unknown emails and wrong passwords take the same time and return the same error, so neither reveals which accounts exist.

## Consequences

- API calls cost one HMAC check and no I/O. Sign-in and refresh are the only authentication paths that touch the database.
- A revoked session's access token stays valid for at most 10 minutes. For this product that is an acceptable window. Permission checks (ADR-0007) are evaluated live, so a removed member loses access immediately.
- Rotating the signing key (`Cadence:Auth:SigningKey`) invalidates all access tokens within one lifetime. Users are refreshed transparently, because refresh tokens don't depend on the key.
- Revoked and expired refresh tokens accumulate. A retention job removes them in M8.
- The auth endpoints are rate-limited per IP (10 per minute for sign-in and password flows, 60 per minute for refresh), in addition to Identity's account lockout.
