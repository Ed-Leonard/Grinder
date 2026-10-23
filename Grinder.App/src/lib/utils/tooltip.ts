import type { Action } from "svelte/action";

export const tooltip: Action<HTMLElement, string> = (node, text) => {
  let tooltipEl: HTMLDivElement | null = null;
  let showTimeout: ReturnType<typeof setTimeout> | null = null;
  const DELAY = 200;

  function show() {
    tooltipEl = document.createElement("div");
    tooltipEl.textContent = text;

    // use a custom z value to ensure it appears above popups and modals etc.
    tooltipEl.className =
      "absolute z-[100] px-4 py-2 text-sm bg-surface rounded-lg shadow-sm pointer-events-none max-w-48";
    tooltipEl.style.opacity = "0";
    tooltipEl.style.transition = `opacity ${300}ms ease`;
    document.body.appendChild(tooltipEl);
    positionTooltip();

    // have to wait for next frame
    requestAnimationFrame(() => {
      requestAnimationFrame(() => {
        if (tooltipEl) tooltipEl.style.opacity = "1";
      });
    });
  }

  function scheduleShow() {
    clearTimeout(showTimeout ?? undefined);
    showTimeout = setTimeout(show, DELAY);
  }

  function positionTooltip() {
    if (!tooltipEl) return;
    const rect = node.getBoundingClientRect();
    const tRect = tooltipEl.getBoundingClientRect();
    const margin = 6;
    const top =
      rect.top < tRect.height + margin
        ? rect.bottom + margin
        : rect.top - tRect.height - margin;
    const centered = rect.left + rect.width / 2 - tRect.width / 2;
    const left =
      centered < 0
        ? rect.left
        : centered + tRect.width > window.innerWidth
          ? rect.right - tRect.width
          : centered;
    tooltipEl.style.top = `${top}px`;
    tooltipEl.style.left = `${left}px`;
  }

  function hide() {
    if (showTimeout) {
      clearTimeout(showTimeout);
      showTimeout = null;
    }
    tooltipEl?.remove();
    tooltipEl = null;
  }

  node.addEventListener("mouseenter", scheduleShow);
  node.addEventListener("mouseleave", hide);

  return {
    update(newText: string) {
      text = newText;
      if (tooltipEl) tooltipEl.textContent = newText;
    },
    destroy() {
      hide();
      node.removeEventListener("mouseenter", scheduleShow);
      node.removeEventListener("mouseleave", hide);
    },
  };
};
