import { useSyncExternalStore } from 'react';
import { session, type SessionState } from './session';

const subscribe = (listener: () => void) => session.subscribe(listener);
const getSnapshot = () => session.get();

/** The current session; re-renders when the user signs in or out. */
export function useSession(): SessionState {
  return useSyncExternalStore(subscribe, getSnapshot);
}
