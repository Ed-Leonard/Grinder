<script lang="ts">
  import { untrack, type Snippet } from "svelte";

  type Vertical = "auto" | "up" | "down";
  type Horizontal = "auto" | "left" | "right" | "center";

  let {
    open = $bindable(false),
    trigger,
    content,
    vertical = "auto",
    horizontal = "auto",
    viewportPadding = 16,
    class: className = "",
    panelClass = "",
    onopen,
    onclose,
  }: {
    open?: boolean;
    trigger: Snippet<
      [{ open: boolean; toggle: () => void; close: () => void }]
    >;
    content: Snippet<[{ close: () => void }]>;
    vertical?: Vertical;
    horizontal?: Horizontal;
    viewportPadding?: number;
    class?: string;
    panelClass?: string;
    onopen?: () => void;
    onclose?: () => void;
  } = $props();

  let rootEl = $state<HTMLDivElement | null>(null);
  let panelEl = $state<HTMLDivElement | null>(null);

  let openUpward = $state(false);
  let alignment = $state<"left" | "right" | "center">("right");
  let centerOffset = $state(0);

  function toggle() {
    open = !open;
  }

  function close() {
    open = false;
  }

  function place() {
    if (!rootEl || !panelEl) return;

    const anchor = rootEl.getBoundingClientRect();
    const panelWidth = panelEl.offsetWidth;
    const height = panelEl.offsetHeight + viewportPadding;

    if (vertical === "auto") {
      const below = window.innerHeight - anchor.bottom;
      const above = anchor.top;
      // only flip if it doesn't fit below AND there's more room above
      openUpward = below < height && above > below;
    } else {
      openUpward = vertical === "up";
    }

    if (horizontal === "auto") {
      const toTheRight = window.innerWidth - anchor.left;
      const toTheLeft = anchor.right;
      const needed = panelWidth + viewportPadding;

      if (toTheRight >= needed) alignment = "right";
      else if (toTheLeft >= needed) alignment = "left";
      else alignment = "center"; // too narrow either way, fall back to the middle
    } else {
      alignment = horizontal;
    }

    if (alignment === "center") {
      // centre on the trigger, then clamp so the panel stays inside the viewport
      const centred = anchor.left + anchor.width / 2 - panelWidth / 2;
      const maxLeft = window.innerWidth - panelWidth - viewportPadding;
      const clamped = Math.max(viewportPadding, Math.min(centred, maxLeft));
      centerOffset = clamped - anchor.left;
    }
  }

  $effect(() => {
    if (open && panelEl) place();
  });

  // fire onopen / onclose on transitions, including externally bound changes
  let wasOpen = false;
  $effect(() => {
    if (open === wasOpen) return;
    wasOpen = open;
    untrack(() => (open ? onopen?.() : onclose?.()));
  });

  function handleWindowClick(e: MouseEvent) {
    if (!open) return;
    // wrapper contains both trigger and panel
    if (rootEl?.contains(e.target as Node)) return;
    close();
  }

  function handleWindowKeydown(e: KeyboardEvent) {
    if (open && e.key === "Escape") close();
  }
</script>

<svelte:window onclick={handleWindowClick} onkeydown={handleWindowKeydown} />

<div bind:this={rootEl} class="relative inline-block {className}">
  {@render trigger({ open, toggle, close })}

  {#if open}
    <div
      bind:this={panelEl}
      style:left={alignment === "center" ? `${centerOffset}px` : undefined}
      class="absolute z-50 overflow-hidden rounded-lg border border-surface-alt bg-surface shadow-lg
        {openUpward ? 'bottom-full mb-1' : 'top-full mt-1'}
        {alignment === 'left' ? 'right-0' : ''}
        {alignment === 'right' ? 'left-0' : ''}
        {panelClass}"
    >
      {@render content({ close })}
    </div>
  {/if}
</div>
