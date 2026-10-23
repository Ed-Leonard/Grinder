<script lang="ts">
  import XIcon from "$lib/components/svgs/xicon.svelte";
  import { tooltip } from "$lib/utils/tooltip";

  interface Props {
    open?: boolean;
    year?: number | null;
    onview?: () => void;
    ondismiss?: () => void;
  }

  let { open = $bindable(false), year = null, onview, ondismiss }: Props =
    $props();

  function view() {
    open = false;
    onview?.();
  }

  function dismiss() {
    open = false;
    ondismiss?.();
  }

  function handleKeydown(e: KeyboardEvent) {
    if (!open) return;
    if (e.key === "Escape") dismiss();
  }
</script>

<svelte:window onkeydown={handleKeydown} />

{#if open}
  <div
    class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4"
  >
    <div
      class="relative w-full max-w-lg rounded-2xl border border-surface-alt bg-surface p-3 text-center shadow-sm"
    >
      <div class="flex flex-col items-center gap-5">
        <div class="flex w-full items-center justify-between gap-3">
          <span class="icon-button invisible shrink-0" aria-hidden="true">
            <XIcon size={1.5} />
          </span>
          <h2>Grinder Wrapped</h2>
          <button
            class="icon-button shrink-0"
            onclick={dismiss}
            aria-label="Dismiss Grinder Wrapped"
            use:tooltip={"Dismiss"}
          >
            <XIcon size={1.5} />
          </button>
        </div>
        <p class="text-sm text-text-muted">Your {year} Grinder Wrapped is ready.</p>
        <button class="primary-button w-full" onclick={view}>
          View Grinder Wrapped
        </button>
      </div>
    </div>
  </div>
{/if}
