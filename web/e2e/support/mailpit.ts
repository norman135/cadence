import { expect, type APIRequestContext } from '@playwright/test';

const mailpitUrl = process.env.E2E_MAILPIT_URL ?? 'http://localhost:8025';

interface MessageSummary {
  ID: string;
  To: { Address: string }[];
  Subject: string;
}

/**
 * Waits for the newest email sent to `address` and returns the first link in it whose path
 * starts with `path`, e.g. `/confirm-email`. Emails are sent from a background queue, so the
 * message can arrive a moment after the request that triggered it.
 */
export async function linkFromEmail(
  request: APIRequestContext,
  address: string,
  path: string,
): Promise<string> {
  let messageId: string | undefined;

  await expect
    .poll(
      async () => {
        const response = await request.get(
          `${mailpitUrl}/api/v1/search?query=${encodeURIComponent(`to:"${address}"`)}`,
        );
        const { messages } = (await response.json()) as { messages: MessageSummary[] };
        messageId = messages[0]?.ID;
        return messageId;
      },
      { message: `an email to ${address}`, timeout: 15_000 },
    )
    .toBeTruthy();

  const response = await request.get(`${mailpitUrl}/api/v1/message/${messageId ?? ''}`);
  const { Text } = (await response.json()) as { Text: string };

  const link = Text.match(/https?:\/\/\S+/g)?.find((url) => new URL(url).pathname === path);
  expect(link, `a ${path} link in the email to ${address}`).toBeDefined();

  // Links point at the public URL; keep only the path and query so the tests follow them on
  // whichever origin they run against.
  const url = new URL(link ?? '');
  return `${url.pathname}${url.search}`;
}
