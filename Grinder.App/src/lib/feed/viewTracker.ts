import { markTasksViewed } from "$lib/api/feed";

const FLUSH_INTERVAL_MS = 4000;

const pending = new Set<number>();
let timer: ReturnType<typeof setInterval> | null = null;

async function flush() {
  if (pending.size === 0) return;
  const ids = Array.from(pending);
  pending.clear();

  try {
    await markTasksViewed(ids);
  } catch {
    // best-effort — a missed mark just means the post resurfaces later, not worth retrying
  }
}

export function markSeen(id: number) {
  pending.add(id);

  if (!timer) {
    timer = setInterval(flush, FLUSH_INTERVAL_MS);
  }
}

export function flushNow() {
  return flush();
}
