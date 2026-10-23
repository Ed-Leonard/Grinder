<script lang="ts">
  import { createComment, type CommentResponse } from "$lib/api/comment";
  import autogrow from "$lib/utils/autogrow";
  import ErrorPopup from "$lib/components/utils/Error.svelte";

  let {
    taskId,
    onCommentCreated,
    isFeed = false,
  }: {
    taskId: number;
    onCommentCreated?: (comment: CommentResponse) => void;
    isFeed?: boolean;
  } = $props();

  let content = $state("");
  let isSubmitting = $state(false);
  let error = $state("");

  async function handleCreateComment() {
    if (isSubmitting || !content.trim()) return;

    isSubmitting = true;
    error = "";

    try {
      const comment = await createComment({
        taskId,
        content: content.trim(),
        parentCommentId: null,
      });

      content = "";
      // Notify parent that a comment was created
      onCommentCreated?.(comment);
    } catch (e) {
      error = e instanceof Error ? e.message : "Failed to create comment";
    } finally {
      isSubmitting = false;
    }
  }

  function handleKeydown(event: KeyboardEvent) {
    if (event.key === "Enter" && !event.shiftKey) {
      event.preventDefault();
      handleCreateComment();
    }
  }
</script>

<div>
  <div
    class="flex flex-col md:flex-row gap-3 items-end md:items-center {isFeed
      ? 'py-1'
      : 'py-4 border-b border-surface-alt mt-2'}"
  >
    <div class="w-full floating relative">
      <textarea
        use:autogrow={content}
        bind:value={content}
        onkeydown={handleKeydown}
        placeholder="Post a comment..."
        maxlength="2048"
        rows="1"
        id="createComment"
        class="text-area-field peer"
      ></textarea>
      <label class="input-label" for="createComment">
        Comment (0-2048 Characters) {content.length > 0
          ? "(" + content.length + " / 2048)"
          : ""}
      </label>
    </div>

    <button
      onclick={handleCreateComment}
      disabled={isSubmitting || !content.trim()}
      class="primary-button"
    >
      {isSubmitting ? "Posting..." : "Post"}
    </button>
  </div>
  {#if error}
    <ErrorPopup {error} />
  {/if}
</div>
