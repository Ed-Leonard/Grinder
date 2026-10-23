import { writable } from "svelte/store";

export let taskUpdateTick = writable(0);

export function notifyTaskUpdated() {
  taskUpdateTick.update((n) => n + 1);
}
