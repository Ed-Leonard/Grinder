<script lang="ts">
  import type { FeedItem } from "$lib/api/feed";
  import ProfileIcon from "$lib/components/svgs/profileicon.svelte";
  import TaskIcon from "$lib/components/tasks/TaskIcon.svelte";
  import { timeAgo, formatDate } from "$lib/utils/dateUtils.svelte";
  import { colourForStatus, statusLabel } from "$lib/utils/taskUtils";
  import CreateComment from "../Comments/CreateComment.svelte";
  import ListComment from "../Comments/ListComment.svelte";
  import CommentIcon from "../svgs/commenticon.svelte";
  import { resolve } from "$app/paths";
  import { markSeen } from "$lib/feed/viewTracker";
  import { onMount } from "svelte";
  import { UpdatedFields } from "$lib/types";
  import TaskReactions from "../reactions/TaskReactions.svelte";
  import { user } from "$lib/stores/auth";
  import BadgeIcon from "$lib/components/svgs/badges/BadgeIcon.svelte";
  import { slide } from "svelte/transition";
  import TaskDate from "$lib/components/tasks/TaskDate.svelte";
  import AttachmentView from "$lib/components/media/AttachmentView.svelte";
  import { tooltip } from "$lib/utils/tooltip";
  import ShareButton from "../ShareButton.svelte";

  let { item }: { item: FeedItem } = $props();
  let cardEl = $state<HTMLElement>();

  let showUpdated = $state(false);

  let showComments = $state(false);
  let listCommentRef: ListComment | null = $state(null);

  onMount(() => {
    if (!cardEl) return;
    let fired = false;

    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting && !fired) {
          fired = true;
          markSeen(item.id);
          observer.disconnect();
        }
      },
      { threshold: 0.6 },
    );

    observer.observe(cardEl);
    return () => observer.disconnect();
  });

  const DESCRIPTION_LIMIT = 240;
  let expanded = $state(false);

  const isLong = $derived((item.description?.length ?? 0) > DESCRIPTION_LIMIT);
  const displayedDescription = $derived(
    !item.description
      ? ""
      : expanded || !isLong
        ? item.description
        : item.description.slice(0, DESCRIPTION_LIMIT).trimEnd() + "…",
  );

  function describeChange(item: FeedItem, field: UpdatedFields): string {
    switch (field) {
      case UpdatedFields.Title:
        return "Title updated";
      case UpdatedFields.Description:
        return "Description updated";
      case UpdatedFields.Start:
        return item.startDate && item.startTime
          ? `Start date changed to ${formatDate(item.startDate, item.startTime)}`
          : "Start date updated";
      case UpdatedFields.Due:
        return item.dueDate
          ? `Due date changed to ${formatDate(item.dueDate, item.dueTime)}`
          : "Due date updated";
      case UpdatedFields.Status:
        return `Status changed to ${statusLabel(item.itemStatus).toLocaleLowerCase()}`;
      case UpdatedFields.Categories:
        return "Categories updated";
      case UpdatedFields.ParentTask:
        return "Parent task updated";
      default:
        return "Updated";
    }
  }

  function toggleComments() {
    showComments = !showComments;
  }
</script>

<article
  class="relative p-4 flex flex-col gap-2 border-b-2 md:border-b border-surface-alt-hover border-l-2 md:border-l-4 card"
  bind:this={cardEl}
  style="border-left-color: {colourForStatus(item.itemStatus)}"
>
  <!-- the negative margin seems weird but actually works well here it allows us to keep consistent padding on all the elements while avoiding triggering hover over the comments section -->
  <div
    class="relative flex flex-col gap-2 [&:has(.task-link:hover)]:bg-surface-hover transition-colors -m-4 p-4 {showComments
      ? 'rounded-t-[inherit]'
      : 'rounded-[inherit]'}"
  >
    <a
      href={resolve(`/tasks/${item.id}` as any)}
      aria-label="Go to task"
      class="absolute inset-0 task-link"
    ></a>
    <div class="flex justify-between items-center min-w-0">
      <div class="flex items-center gap-2 min-w-0">
        <ProfileIcon
          size={2.5}
          userId={item.userId}
          alt={item.userDisplayName}
        />
        <div class="flex flex-col min-w-0">
          <div class="flex items-center gap-1 min-w-0">
            <a
              href={resolve(`/profile/${item.userId}`)}
              class="font-semibold hover:underline z-10 truncate"
            >
              {item.userDisplayName}
            </a>
            {#if item.selectedBadgeIcon}
              <span use:tooltip={item.selectedBadgeName || ""}>
                <BadgeIcon icon={item.selectedBadgeIcon} size={1.5} />
              </span>
            {/if}
          </div>
          <span class="text-xs text-text-muted">
            {timeAgo(item.createdAt)}
          </span>
        </div>
      </div>

      <div class="inline-flex gap-2 items-center">
        <span class="text-xs text-text-muted truncate">
          {#if item.isUpdated && item.changedFields}
            <span class="font-medium items-center">
              {#if item.changedFields.length > 1}
                <button
                  class="flex items-center gap-1"
                  onmouseenter={() => (showUpdated = true)}
                  onmouseleave={() => (showUpdated = false)}
                  onclick={() => (showUpdated = !showUpdated)}
                >
                  {item.changedFields.length} fields updated
                  <span
                    class="h-1 w-1 mb-2 rounded-full bg-red-600"
                    aria-hidden="true"
                  ></span>
                </button>
              {:else}
                <div class="p-1">
                  {describeChange(item, item.changedFields[0])}
                </div>
              {/if}
              {#if showUpdated}
                <div class="absolute z-50 shadow-sm bg-surface rounded-md p-2">
                  {#each item.changedFields as field}
                    <div class="p-1">{describeChange(item, field)}</div>
                  {/each}
                </div>
              {/if}
            </span>
          {/if}
        </span>
        <span
          class="items-center inline-flex gap-2 rounded-full px-4 py-2 text-sm font-medium text-white shrink-0"
          style="background-color: {colourForStatus(item.itemStatus)}"
        >
          <TaskIcon status={item.itemStatus} colour="currentColor" />
          {statusLabel(item.itemStatus)}
        </span>
      </div>
    </div>

    <div class="flex flex-wrap justify-between items-baseline gap-2">
      <a
        class="font-serif w-min font-semibold text-2xl wrap-anywhere text-left truncate hover:underline"
        href={resolve(`/tasks/${item.id}` as any)}
      >
        {item.title}
      </a>
      {#if item.startDate}
        <p class="text-sm text-text-muted">
          <TaskDate date={item.startDate ?? ""} time={item.startTime ?? ""} />
          -
          <TaskDate date={item.dueDate} time={item.dueTime ?? ""} />
        </p>
      {:else}
        <p class="text-sm text-text-muted">
          <TaskDate date={item.dueDate} time={item.dueTime ?? ""} />
        </p>
      {/if}
    </div>

    {#if item.taskAttachments.length > 0}
      <AttachmentView attachments={item.taskAttachments} />
    {/if}

    {#if item.description}
      <p class="whitespace-pre-wrap wrap-anywhere">
        {displayedDescription}
        {#if isLong}
          <button
            type="button"
            class="ml-1 font-medium hover:underline z-50"
            onclick={() => (expanded = !expanded)}
          >
            {expanded ? "Show less" : "Show more"}
          </button>
        {/if}
      </p>
    {/if}

    <div class="flex flex-wrap gap-2">
      {#each item.categories as category (category.id)}
        <span
          class="rounded-full bg-surface-alt border border-surface-alt-hover px-3 py-0.5 text-xs font-medium text-text-muted"
        >
          {category.name}
        </span>
      {/each}
    </div>

    <div class="flex flex-row-reverse flex-wrap items-center gap-2">
      <ShareButton taskId={item.id} />
      <button
        onclick={toggleComments}
        aria-label="toggle comments"
        use:tooltip={showComments ? "Hide comments" : "Show comments"}
        class="icon-button shrink-0 z-10
          {showComments ? 'bg-brand text-surface' : ''}"
      >
        <CommentIcon />
      </button>
      <TaskReactions taskId={item.id} initialReactions={item.reactions ?? []} />
    </div>
  </div>

  {#if showComments}
    <div
      transition:slide={{ duration: 250 }}
      class="relative flex flex-col gap-y-4 py-2 border-t border-surface-alt mt-4"
    >
      <div class="overflow-y-auto">
        <ListComment
          bind:this={listCommentRef}
          isFeed={true}
          taskId={item.id}
          currentUserId={$user?.id ?? null}
        />
      </div>
      <div>
        <CreateComment
          isFeed={true}
          taskId={item.id}
          onCommentCreated={(comment) => listCommentRef?.addComment(comment)}
        />
      </div>
    </div>
  {/if}
</article>
