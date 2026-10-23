<script lang="ts">
  import { onMount } from "svelte";
  import { formatDateTime } from "$lib/utils/dateUtils.svelte";
  import {
    getCommentsByTaskId,
    getRepliesByCommentId,
    type CommentResponse,
    deleteComment,
    createComment,
  } from "$lib/api/comment";
  import ProfileIcon from "$lib/components/svgs/profileicon.svelte";
  import RubbishBinIcon from "$lib/components/svgs/rubbishbin.svelte";
  import Modal from "$lib/components/utils/Modal.svelte";
  import { resolve } from "$app/paths";
  import autogrow from "$lib/utils/autogrow";
  import { tooltip } from "$lib/utils/tooltip";
  import ErrorPopup from "$lib/components/utils/Error.svelte";
  import { SvelteSet, SvelteMap } from "svelte/reactivity";

  const REPLIES_PAGE_SIZE = 10;

  let {
    taskId,
    isFeed,
    currentUserId,
    showHeading = true,
    onCommentCountChange,
  }: {
    taskId: number;
    isFeed: boolean;
    currentUserId: number | null;
    showHeading?: boolean;
    onCommentCountChange?: (count: number) => void;
  } = $props();

  let comments = $state<CommentResponse[]>([]);
  let totalCount = $state(0);
  let isLoading = $state(true);
  let error = $state("");

  let pendingDeleteCommentId = $state<number | null>(null);
  let isDeletingComment = $state(false);

  let replyingToCommentId = $state<number | null>(null);
  let replyContent = $state("");
  let isSubmittingReply = $state(false);
  let replyError = $state("");

  let expandedReplies = $state(new SvelteSet<number>());
  let loadingReplies = $state(new SvelteSet<number>());
  let replyHasMore = $state(new SvelteMap<number, boolean>());

  function notifyCommentCount() {
    onCommentCountChange?.(totalCount);
  }

  async function toggleReplies(commentId: number) {
    if (expandedReplies.has(commentId)) {
      expandedReplies.delete(commentId);
    } else {
      expandedReplies.add(commentId);

      const comment = findCommentById(comments, commentId);
      if (comment && comment.replies.length === 0) {
        await fetchReplies(commentId);
      }
    }
  }

  async function fetchReplies(commentId: number) {
    loadingReplies.add(commentId);
    error = "";

    try {
      const comment = findCommentById(comments, commentId);
      if (!comment) return;

      const skip = comment.replies.length;
      const result = await getRepliesByCommentId(
        commentId,
        skip,
        REPLIES_PAGE_SIZE,
      );

      comment.replies = [...comment.replies, ...result.replies];
      replyHasMore.set(commentId, result.hasMore);
      comments = [...comments];
    } catch (e) {
      error = e instanceof Error ? e.message : "Failed to load replies";
    } finally {
      loadingReplies.delete(commentId);
    }
  }

  async function loadMoreReplies(commentId: number) {
    await fetchReplies(commentId);
  }

  onMount(async () => {
    isLoading = true;
    error = "";
    try {
      const result = await getCommentsByTaskId(taskId);
      comments = result.comments;
      totalCount = result.totalCount;
      notifyCommentCount();
    } catch (e) {
      error = e instanceof Error ? e.message : "Failed to load comments";
    } finally {
      isLoading = false;
    }
  });

  export function addComment(comment: CommentResponse) {
    comments = [comment, ...comments];
    totalCount += 1;
    notifyCommentCount();
  }

  function removeCommentFromTree(
    commentList: CommentResponse[],
    id: number,
  ): boolean {
    const idx = commentList.findIndex((c) => c.id === id);
    if (idx !== -1) {
      commentList.splice(idx, 1);
      return true;
    }
    for (const c of commentList) {
      if (removeCommentFromTree(c.replies, id)) {
        c.replyCount = Math.max(0, c.replyCount - 1);
        return true;
      }
    }
    return false;
  }

  function updateCommentInTree(
    commentList: CommentResponse[],
    updated: CommentResponse,
  ): boolean {
    const idx = commentList.findIndex((c) => c.id === updated.id);
    if (idx !== -1) {
      commentList[idx] = {
        ...commentList[idx],
        ...updated,
        replies: commentList[idx].replies,
      };
      return true;
    }
    for (const c of commentList) {
      if (updateCommentInTree(c.replies, updated)) return true;
    }
    return false;
  }

  function requestDeleteComment(commentId: number) {
    pendingDeleteCommentId = commentId;
  }

  function cancelDeleteComment() {
    pendingDeleteCommentId = null;
  }

  async function confirmDeleteComment() {
    if (pendingDeleteCommentId == null) {
      return;
    }
    const commentId = pendingDeleteCommentId;
    isDeletingComment = true;

    try {
      const result = await deleteComment(commentId);
      let applied = false;

      if (result.removed) {
        applied = updateCommentInTree(comments, {
          id: commentId,
          isDeleted: true,
          content: "",
        } as CommentResponse);
        comments = [...comments];
        // Hard delete: the row is actually gone, so the backend's total
        // count for this task just dropped by one too.
        totalCount = Math.max(0, totalCount - 1);
        notifyCommentCount();
      } else if (result.comment) {
        applied = updateCommentInTree(comments, result.comment);
        // Soft delete: the row stays (shown as "Deleted comment"), so the
        // backend's total count is unchanged — don't touch totalCount.
      }

      if (!applied) {
        error =
          "Comment was deleted, but the list may be out of sync. Please refresh the page.";
      }

      comments = [...comments];
    } catch (e) {
      error = e instanceof Error ? e.message : "Failed to delete comment";
    } finally {
      isDeletingComment = false;
      pendingDeleteCommentId = null;
    }
  }

  function startReply(commentId: number) {
    if (replyingToCommentId === commentId) {
      cancelReply();
      return;
    }
    replyingToCommentId = commentId;
    replyContent = "";
    replyError = "";
  }

  function cancelReply() {
    replyingToCommentId = null;
    replyContent = "";
    replyError = "";
  }

  function findCommentById(
    commentList: CommentResponse[],
    id: number,
  ): CommentResponse | null {
    for (const comment of commentList) {
      if (comment.id === id) return comment;
      const found = findCommentById(comment.replies, id);
      if (found) return found;
    }
    return null;
  }

  async function submitReply() {
    if (
      isSubmittingReply ||
      !replyContent.trim() ||
      replyingToCommentId === null
    ) {
      return;
    }

    isSubmittingReply = true;
    replyError = "";
    const parentId = replyingToCommentId;

    try {
      const newReply = await createComment({
        taskId,
        content: replyContent.trim(),
        parentCommentId: parentId,
      });

      replyContent = "";
      replyingToCommentId = null;

      const parent = findCommentById(comments, parentId);
      if (parent) {
        parent.replies = [...parent.replies, newReply];
        parent.replyCount += 1;
      } else {
        error =
          "Reply was posted, but the parent comment may be out of sync. Please refresh the page.";
      }

      expandedReplies.add(parentId);

      comments = [...comments];
      totalCount += 1;
      notifyCommentCount();
    } catch (e) {
      replyError = e instanceof Error ? e.message : "Failed to submit reply";
    } finally {
      isSubmittingReply = false;
    }
  }

  function handleReplyKeydown(event: KeyboardEvent) {
    if (event.key === "Enter" && !event.shiftKey) {
      event.preventDefault();
      submitReply();
    }
    if (event.key === "Escape") {
      cancelReply();
    }
  }

  function autofocus(node: HTMLTextAreaElement) {
    node.focus();
  }
</script>

{#snippet renderComment(
  comment: CommentResponse,
  depth: number,
  replyingToName?: string,
  replyingToUserId?: number,
)}
  <div class="py-2 pr-4 {depth > 0 ? 'ml-10' : ''}">
    <div class="border-l-2 pl-4 border-surface-alt">
      <div class="flex items-start gap-3">
        {#if comment.isDeleted}
          <div
            class="shrink-0 flex items-center justify-center"
            style="width: {isFeed ? 2 : 3}rem; height: {isFeed ? 2 : 3}rem;"
            aria-hidden="true"
          >
            <RubbishBinIcon size={isFeed ? 1 : 1.25} />
          </div>
        {:else}
          <ProfileIcon size={isFeed ? 2 : 3} userId={comment.author.id} />
        {/if}

        <div class="min-w-0 flex-1">
          {#if comment.isDeleted}
            <p
              class="text-sm italic text-text-muted {isFeed
                ? 'mt-1.5'
                : 'mt-4'}"
            >
              Deleted comment
            </p>
          {:else}
            <div class="flex items-center gap-2">
              <a
                href={resolve(`/profile/${comment.author.id}`)}
                class="font-semibold hover:underline"
              >
                {comment.author.displayName}
              </a>
              <span
                class="text-text-muted {isFeed ? 'text-xs ' : 'text-sm '}"
              >
                {formatDateTime(comment.createdAt)}
              </span>
            </div>
            <p class="text-text {isFeed ? 'text-sm ' : ''} wrap-anywhere">
              {#if replyingToName}
                <a
                  href={resolve(`/profile/${replyingToUserId ?? comment.author.id}`)}
                  class="text-blue-500 font-medium hover:underline">@{replyingToName}</a
                >{" "}
              {/if}
              {comment.content}
            </p>
          {/if}

          <div class="mt-2 flex items-center gap-3">
            {#if !comment.isDeleted}
              <button
                type="button"
                class="text-sm text-text-muted hover:underline"
                onclick={() => startReply(comment.id)}
              >
                Reply
              </button>
            {/if}
            {#if comment.replyCount > 0}
              <button
                type="button"
                class="text-sm text-text-muted hover:text-text"
                onclick={() => toggleReplies(comment.id)}
              >
                {expandedReplies.has(comment.id) ? "Hide" : "Show"}
                {comment.replyCount === 1
                  ? "1 reply"
                  : `${comment.replyCount} replies`}
                {expandedReplies.has(comment.id) ? "▲" : "▼"}
              </button>
            {/if}
          </div>
        </div>

        {#if !comment.isDeleted && comment.author.id === currentUserId}
          <button
            type="button"
            aria-label="Delete comment"
            use:tooltip={"Delete comment"}
            class="shrink-0 hover:text-red-500 icon-button"
            onclick={() => requestDeleteComment(comment.id)}
          >
            <RubbishBinIcon size={isFeed ? 1.25 : 1.5} />
          </button>
        {/if}
      </div>

      {#if replyingToCommentId === comment.id}
        <div
          class="mt-4 flex flex-col md:flex-row gap-2 items-end md:items-center"
          style="margin-left: {isFeed ? 2.75 : 3.75}rem;"
        >
          <div class="w-full floating relative">
            <textarea
              use:autogrow={replyContent}
              use:autofocus
              bind:value={replyContent}
              onkeydown={handleReplyKeydown}
              rows="1"
              maxlength="2048"
              placeholder="Post a reply..."
              id="replyToComment"
              class="text-area-field peer"
            ></textarea>
            <label class="input-label" for="replyToComment">
              Comment (1-2048 Characters) {replyContent.length > 1
                ? "(" + replyContent.length + " / 2048)"
                : ""}
            </label>
          </div>
          <button
            onclick={submitReply}
            disabled={isSubmittingReply || !replyContent.trim()}
            class="primary-button py-2"
          >
            {isSubmittingReply ? "Posting..." : "Post"}
          </button>
        </div>
        {#if replyError}
          <ErrorPopup error={replyError} />
        {/if}
      {/if}
    </div>
  </div>

  {#if expandedReplies.has(comment.id)}
    {#if loadingReplies.has(comment.id) && comment.replies.length === 0}
      <p class="py-2 text-sm text-text-muted" style="margin-left: 3.5rem;">
        Loading replies...
      </p>
    {/if}

    {#each comment.replies as reply (reply.id)}
      {@render renderComment(
        reply,
        depth + 1,
        comment.author.displayName,
        comment.author.id,
      )}
    {/each}

    {#if replyHasMore.get(comment.id)}
      <div style="margin-left: 3.5rem;" class="py-1">
        <button
          type="button"
          class="ml-11 text-sm text-text-muted hover:text-text hover:underline"
          disabled={loadingReplies.has(comment.id)}
          onclick={() => loadMoreReplies(comment.id)}
        >
          {loadingReplies.has(comment.id) ? "Loading..." : "Load more replies"}
        </button>
      </div>
    {/if}
  {/if}
{/snippet}

<div>
  {#if showHeading}
    <h2 class="mb-2 font-semibold text-left {isFeed ? "text-lg" : ""}">Comments ({totalCount})</h2>
  {/if}

  <div class={isFeed ? "max-h-100" : "md:max-h-200"}>
    {#if isLoading}
      <p class="py-2 text-center text-sm text-text-muted">
        Loading comments...
      </p>
    {:else if comments.length === 0}
      <p class="py-2 text-center text-sm text-text-muted">No comments yet.</p>
    {:else}
      {#each comments as comment (comment.id)}
        {@render renderComment(comment, 0)}
      {/each}
    {/if}
  </div>

  {#if error}
    <ErrorPopup {error} />
  {/if}
</div>

{#if pendingDeleteCommentId !== null}
  <Modal onClose={cancelDeleteComment} maxWidth="max-w-sm" layer="z-50">
    {#snippet header()}
      <div>
        <h2 class="font-semibold justify-self-start">Delete comment?</h2>
      </div>
    {/snippet}

    <div class="px-6 py-4 text-text-muted">
      <p>Are you sure you want to delete this comment?</p>
      <p>This action cannot be undone.</p>
    </div>

    {#snippet footer()}
      <div class="ml-auto flex items-center gap-3">
        <button
          type="button"
          class="secondary-button"
          onclick={cancelDeleteComment}
          disabled={isDeletingComment}
        >
          Cancel
        </button>
        <button
          type="button"
          class="destructive-button"
          onclick={confirmDeleteComment}
          disabled={isDeletingComment}
        >
          {isDeletingComment ? "Deleting..." : "Delete"}
        </button>
      </div>
    {/snippet}
  </Modal>
{/if}