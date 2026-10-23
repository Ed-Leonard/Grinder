<script lang="ts">
  import type { Snippet } from "svelte";
  import { portal } from "$lib/utils/portal";

  let {
    header,
    children,
    footer,
    onClose,
    onkeydown,
    maxWidth = "max-w-md",
    layer = "z-50",
  }: {
    header: Snippet;
    children: Snippet;
    footer?: Snippet;
    onClose: () => void;
    onkeydown?: (event: KeyboardEvent) => void;
    maxWidth?: string;
    layer?: string;
  } = $props();

  let dialogElement: HTMLElement | null = $state(null);

  $effect(() => {
    dialogElement?.focus();
  });

  function handleKeydown(event: KeyboardEvent) {
    if (event.key === "Escape") {
      onClose();
    }
    onkeydown?.(event);
  }
</script>

<div
  use:portal
  class="fixed inset-0 {layer} flex items-center justify-center bg-black/60 backdrop-blur-sm"
>
  <section
    bind:this={dialogElement}
    tabindex={-1}
    class="w-full {maxWidth} overflow-hidden rounded-lg bg-surface shadow-xl"
    role="dialog"
    aria-modal="true"
    onkeydown={handleKeydown}
  >
    <header
      class="flex items-center justify-between border-b border-surface-alt px-6 py-4"
    >
      {@render header()}
    </header>

    {@render children()}

    <footer
      class="flex items-center gap-3 border-t border-surface-alt px-6 py-4"
    >
      {#if footer}
        {@render footer()}
      {:else}
        <button type="button" class="secondary-button" onclick={onClose}>
          Close
        </button>
      {/if}
    </footer>
  </section>
</div>
