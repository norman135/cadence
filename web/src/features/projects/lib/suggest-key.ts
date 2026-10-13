/** Mirrors Project.SuggestKey on the server: initials of the words, or a single word's first letters. */
export function suggestKey(name: string) {
  const words = name
    .split(/\s+/)
    .map((word) => word.replace(/[^A-Za-z0-9]/g, '').toUpperCase())
    .filter((word) => /^[A-Z]/.test(word));
  const key =
    words.length === 0
      ? 'PRJ'
      : words.length === 1
        ? (words[0] ?? '').slice(0, 3)
        : words
            .slice(0, 10)
            .map((word) => word[0])
            .join('');
  return key.length >= 2 ? key : `${key}X`.slice(0, 2);
}
