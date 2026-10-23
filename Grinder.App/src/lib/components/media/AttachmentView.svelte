<script lang="ts">
  import { taskAttachmentUrl } from "$lib/api/media";
  import type { TaskAttachment } from "$lib/types";
  import type { FeedAttachment } from "$lib/api/feed";
  import Leftarrow from "$lib/components/svgs/leftarrow.svelte";
  import Rightarrow from "$lib/components/svgs/rightarrow.svelte";
  import { tooltip } from "$lib/utils/tooltip";

  let {
    attachments,
    autoplay = true,
  }: { attachments: TaskAttachment[] | FeedAttachment[]; autoplay?: boolean } =
    $props();

  let index = $state(0);

  $effect(() => {
    attachments;
    index = 0;
  });

  let current: TaskAttachment | FeedAttachment | undefined = $derived(
    attachments[index],
  );

  function prev() {
    if (attachments.length === 0) return;
    index = (index - 1 + attachments.length) % attachments.length;
  }

  function next() {
    if (attachments.length === 0) return;
    index = (index + 1) % attachments.length;
  }
</script>

{#if current}
  <div
    class="flex w-full h-80 md:h-112 items-center justify-center relative overflow-hidden rounded-lg border border-surface-alt bg-surface group"
  >
    {#if current.contentType.startsWith("video/")}
      <video
        class="h-full w-full bg-black object-contain"
        src={taskAttachmentUrl(current.id)}
        aria-label={current.originalFileName}
        controls
        {autoplay}
        muted
        preload="metadata"
      >
        <track kind="captions" />
      </video>
    {:else}
      <img
        class="h-full w-full object-contain"
        src={taskAttachmentUrl(current.id)}
        alt={current.originalFileName}
      />
    {/if}

    {#if attachments.length > 1}
      <button
        type="button"
        class="absolute left-2 top-1/2 -translate-y-1/2 icon-button bg-surface-alt hover:bg-surface-alt-hover group-hover:opacity-100 pointer-fine:opacity-0 transition-opacity opacity-80"
        onclick={prev}
        aria-label="Previous attachment"
        use:tooltip={"Previous attachment"}
      >
        <Leftarrow />
      </button>
      <button
        type="button"
        class="absolute right-2 top-1/2 -translate-y-1/2 icon-button bg-surface-alt hover:bg-surface-alt-hover group-hover:opacity-100 pointer-fine:opacity-0 transition-opacity opacity-80"
        onclick={next}
        aria-label="Next attachment"
        use:tooltip={"Next attachment"}
      >
        <Rightarrow />
      </button>
      <div
        class="absolute bottom-2 left-1/2 -translate-x-1/2 rounded-full bg-black/50 px-2 py-0.5 text-xs text-white"
      >
        {index + 1} / {attachments.length}
      </div>
    {/if}
  </div>
{/if}
