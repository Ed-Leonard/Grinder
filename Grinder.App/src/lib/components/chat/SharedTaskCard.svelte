<script lang="ts">
  import { resolve } from "$app/paths";
  import type { SharedTaskPreview } from "$lib/api/directMessages";
  import AttachmentView from "../media/AttachmentView.svelte";
  import Profileicon from "../svgs/profileicon.svelte";
  import Uprightarrow from "../svgs/uprightarrow.svelte";
  import { tooltip } from "$lib/utils/tooltip";

  let { task }: { task: SharedTaskPreview | null } = $props();

  const href = $derived(task ? resolve(`/tasks/${task.id}` as any) : "");
</script>

{#if !task}
  <div class="text-red-500">This task is no longer available</div>
{:else}
  <div class="relative">
    <div
      class={!task.imageAttachment && !task.videoAttachment
        ? "justify-center flex flex-col items-center rounded-lg p-2 md:p-4 overflow-hidden"
        : "absolute top-4 left-4 z-10 w-full pr-8"}
      class:shimmer={!task.imageAttachment && !task.videoAttachment}
    >
      <div class="inline-flex justify-between items-center w-full">
        <div class="inline-flex items-center gap-2 font-semibold text-lg">
          <Profileicon userId={task.userId} />
          {task.userDisplayName}
        </div>
        <a
          class="icon-button bg-[#242426]/30 hover:bg-[#242426] text-white"
          {href}
          use:tooltip={"Go to task"}
        >
          <Uprightarrow />
        </a>
      </div>
      {#if !task.imageAttachment && !task.videoAttachment}
        <div
          class="flex h-full justify-center items-center rounded-lg p-2 md:p-4 overflow-hidden"
        >
          <h5
            class="wrap-anywhere xs:text-lg sm:text-xl lg:text-2xl line-clamp-2"
          >
            {task.title}
          </h5>
        </div>
      {/if}
    </div>
    {#if task.imageAttachment}
      <AttachmentView attachments={[task.imageAttachment]} />
    {:else if task.videoAttachment}
      <AttachmentView autoplay={false} attachments={[task.videoAttachment]} />
    {/if}
  </div>
{/if}

<style>
  .shimmer {
    background: linear-gradient(
      45deg,
      var(--color-surface) 0%,
      var(--color-surface-alt-hover) 47%,
      var(--color-surface-alt-hover) 53%,
      var(--color-surface) 100%
    );
    background-size: 150% 150%;
    animation: gradientShift 4s ease-in-out infinite;
  }

  @keyframes gradientShift {
    0% {
      background-position: 0% 50%;
    }
    50% {
      background-position: 100% 50%;
    }
    100% {
      background-position: 0% 50%;
    }
  }
</style>
