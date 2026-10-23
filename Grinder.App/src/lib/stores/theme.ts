import { writable } from "svelte/store";
import { browser } from "$app/environment";

type Theme = "light" | "dark";

function getInitialTheme(): Theme {
  if (!browser) return "light";

  const stored = localStorage.getItem("theme");
  if (stored === "light" || stored === "dark") return stored;

  return window.matchMedia("(prefers-color-scheme: dark)").matches
    ? "dark"
    : "light";
}

function createThemeStore() {
  const { subscribe, set, update } = writable<Theme>(getInitialTheme());

  return {
    subscribe,
    set: (value: Theme) => {
      if (browser) {
        localStorage.setItem("theme", value);
        document.documentElement.classList.toggle("dark", value === "dark");
      }
      set(value);
    },
    toggle: () => {
      update((current) => {
        const next: Theme = current === "dark" ? "light" : "dark";
        if (browser) {
          localStorage.setItem("theme", next);
          document.documentElement.classList.toggle("dark", next === "dark");
        }
        return next;
      });
    },
  };
}

export const theme = createThemeStore();
