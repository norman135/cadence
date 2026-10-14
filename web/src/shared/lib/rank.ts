/*
 * Fractional-index rank keys, the same algorithm as Cadence.Domain.Issues.Rank on the server
 * (ADR-0010). A key always fits between two others, so dropping a card rewrites one row. Keys
 * compare as plain strings: an integer part whose first character encodes its length (a–z for 2–27
 * characters, A–Z for negative integers), then a base-62 fraction that never ends in "0".
 */

const DIGITS = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz';
const ZERO = '0';
const SMALLEST_INTEGER = `A${ZERO.repeat(26)}`;

const digit = (index: number) => DIGITS.charAt(index);

function integerLength(head: string) {
  if (head >= 'a' && head <= 'z') return head.charCodeAt(0) - 'a'.charCodeAt(0) + 2;
  if (head >= 'A' && head <= 'Z') return 'Z'.charCodeAt(0) - head.charCodeAt(0) + 2;
  throw new Error(`Invalid rank head "${head}"`);
}

function integerPart(key: string) {
  const length = integerLength(key.charAt(0));
  if (length > key.length) throw new Error(`Rank ${key} is shorter than its integer part`);
  return key.slice(0, length);
}

function validate(key: string) {
  if (key === SMALLEST_INTEGER) throw new Error(`Rank ${key} is reserved`);
  const integer = integerPart(key);
  if (key.length > integer.length && key.endsWith(ZERO)) {
    throw new Error(`Rank ${key} has a trailing zero`);
  }
}

/** A fraction strictly between `low` and `high` (open when null). */
function midpoint(low: string, high: string | null): string {
  if (high !== null) {
    let shared = 0;
    while (shared < high.length && (low.charAt(shared) || ZERO) === high.charAt(shared)) shared++;
    if (shared > 0) return high.slice(0, shared) + midpoint(low.slice(shared), high.slice(shared));
  }

  const lowDigit = low ? DIGITS.indexOf(low.charAt(0)) : 0;
  const highDigit = high !== null ? DIGITS.indexOf(high.charAt(0)) : DIGITS.length;
  if (highDigit - lowDigit > 1) return digit(Math.round(0.5 * (lowDigit + highDigit)));
  if (high !== null && high.length > 1) return high.slice(0, 1);
  return digit(lowDigit) + midpoint(low.slice(1), null);
}

function increment(integer: string): string | null {
  const head = integer.charAt(0);
  const digits = integer.slice(1).split('');
  let carry = true;
  for (let index = digits.length - 1; carry && index >= 0; index--) {
    const next = DIGITS.indexOf(digits[index] ?? ZERO) + 1;
    if (next === DIGITS.length) {
      digits[index] = ZERO;
    } else {
      digits[index] = digit(next);
      carry = false;
    }
  }

  if (!carry) return head + digits.join('');
  if (head === 'Z') return `a${ZERO}`;
  if (head === 'z') return null;
  const nextHead = String.fromCharCode(head.charCodeAt(0) + 1);
  if (nextHead > 'a') digits.push(ZERO);
  else digits.pop();
  return nextHead + digits.join('');
}

function decrement(integer: string): string | null {
  const head = integer.charAt(0);
  const digits = integer.slice(1).split('');
  let borrow = true;
  for (let index = digits.length - 1; borrow && index >= 0; index--) {
    const previous = DIGITS.indexOf(digits[index] ?? ZERO) - 1;
    if (previous === -1) {
      digits[index] = digit(DIGITS.length - 1);
    } else {
      digits[index] = digit(previous);
      borrow = false;
    }
  }

  if (!borrow) return head + digits.join('');
  if (head === 'a') return `Z${digit(DIGITS.length - 1)}`;
  if (head === 'A') return null;
  const previousHead = String.fromCharCode(head.charCodeAt(0) - 1);
  if (previousHead < 'Z') digits.push(digit(DIGITS.length - 1));
  else digits.pop();
  return previousHead + digits.join('');
}

/** A key between `before` and `after`; either may be null for an open end. */
export function rankBetween(before: string | null, after: string | null): string {
  if (before !== null) validate(before);
  if (after !== null) validate(after);
  if (before !== null && after !== null && before >= after) {
    throw new Error(`${before} is not below ${after}`);
  }

  if (before === null) {
    if (after === null) return `a${ZERO}`;
    const afterInteger = integerPart(after);
    const afterFraction = after.slice(afterInteger.length);
    if (afterInteger === SMALLEST_INTEGER) return afterInteger + midpoint('', afterFraction);
    if (afterInteger < after) return afterInteger;
    const lower = decrement(afterInteger);
    if (lower === null) throw new Error('No rank fits below the smallest rank');
    return lower;
  }

  const beforeInteger = integerPart(before);
  const beforeFraction = before.slice(beforeInteger.length);
  if (after === null) {
    return increment(beforeInteger) ?? beforeInteger + midpoint(beforeFraction, null);
  }

  const afterInteger = integerPart(after);
  if (beforeInteger === afterInteger) {
    return beforeInteger + midpoint(beforeFraction, after.slice(afterInteger.length));
  }

  const next = increment(beforeInteger);
  if (next === null) throw new Error('No rank fits above the largest rank');
  return next < after ? next : beforeInteger + midpoint(beforeFraction, null);
}
