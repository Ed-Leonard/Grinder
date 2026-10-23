import { SvelteMap } from "svelte/reactivity";

interface Toast {
  message: string;
  timer: ReturnType<typeof setTimeout>;
}

export const toasts = new SvelteMap<string, Toast>();

export function toast(message: string, duration = 3000): void {
  clearTimeout(toasts.get(message)?.timer);
  const timer = setTimeout(() => toasts.delete(message), duration);
  toasts.set(message, { message, timer });
}
