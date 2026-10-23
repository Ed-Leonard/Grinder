<script lang="ts">
  import ListComment from "$lib/components/Comments/ListComment.svelte";
  import CreateComment from "$lib/components/Comments/CreateComment.svelte";
  import Xicon from "../svgs/xicon.svelte";
  import { tooltip } from "$lib/utils/tooltip";

  let {
    taskId,
    isOpen,
    currentUserId,
    onClose,
    onCommentCountChange,
  }: {
    taskId: number;
    isOpen: boolean;
    currentUserId: number | null;
    onClose: () => void;
    onCommentCountChange?: (count: number) => void;
  } = $props();

  let listCommentRef: ListComment | null = $state(null);
  let commentCount = $state(0);

  function updateCommentCount(count: number) {
    commentCount = count;
    onCommentCountChange?.(count);
  }

  let hasOpened = $state(true);
  $effect(() => {
    if (isOpen) hasOpened = true;
  });
</script>

{#if hasOpened}
  <div
    class="relative overflow-hidden overflow-y-auto bg-surface shrink-0 shadow-sm
           w-full xl:h-full md:rounded-b-lg xl:rounded-lg
           transition-[height,width] duration-300 ease-[cubic-bezier(0.33,1,0.68,1)]
           {isOpen ? 'h-96 xl:w-lg' : 'h-0 xl:w-0'}"
    inert={!isOpen}
  >
    <div
      class="absolute left-0 right-0 bottom-0 h-96
             xl:left-auto xl:right-0 xl:top-0 xl:bottom-auto xl:h-full xl:w-lg
             bg-surface flex flex-col md:rounded-b-lg xl:rounded-lg"
    >
      <div
        class="flex items-center justify-between border-b border-surface-alt p-2 shrink-0"
      >
        <h2 class="ml-2">Comments ({commentCount})</h2>
        <button
          type="button"
          aria-label="Close comments"
          use:tooltip={"Close comments"}
          class="icon-button"
          onclick={onClose}
        >
          <Xicon />
        </button>
      </div>
      <div class="flex-1 overflow-y-auto overscroll-contain px-2">
        <ListComment
          bind:this={listCommentRef}
          isFeed={true}
          showHeading={false}
          onCommentCountChange={updateCommentCount}
          {taskId}
          {currentUserId}
        />
      </div>
      <div
        class="border-t border-surface-alt p-4 shrink-0"
        style="padding-bottom: max(0.75rem, env(safe-area-inset-bottom));"
      >
        <CreateComment
          isFeed={true}
          {taskId}
          onCommentCreated={(comment) => listCommentRef?.addComment(comment)}
        />
      </div>
    </div>
  </div>
{/if}
