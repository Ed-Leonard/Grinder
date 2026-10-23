<script lang="ts">
  import type { ClipItem } from "$lib/api/clips";
  import { taskAttachmentUrl } from "$lib/api/media";
  import ProfileIcon from "$lib/components/svgs/profileicon.svelte";
  import Speaker from "$lib/components/svgs/clips/speaker.svelte";
  import { timeAgo } from "$lib/utils/dateUtils.svelte";
  import BadgeIcon from "$lib/components/svgs/badges/BadgeIcon.svelte";
  import TaskReactions from "../reactions/TaskReactions.svelte";
  import ClipCommentsPanel from "$lib/components/Comments/ClipCommentsPanel.svelte";
  import CommentIcon from "../svgs/commenticon.svelte";
  import { resolve } from "$app/paths";
  import { tooltip } from "$lib/utils/tooltip";
  import PlayIcon from "$lib/components/svgs/clips/play.svelte";
  import UprightArrow from "$lib/components/svgs/uprightarrow.svelte";
  import ShareButton from "$lib/components/ShareButton.svelte";

  let {
    item,
    active = false,
    muted = $bindable(true),
    currentUserId,
  }: {
    item: ClipItem;
    active?: boolean;
    muted?: boolean;
    currentUserId: number | null;
  } = $props();

  const TITLE_LIMIT = 50;
  const DESCRIPTION_LIMIT = 100;
  let expanded = $state(false);
  let videoEl = $state<HTMLVideoElement>();
  let showComments = $state(false);
  let isPaused = $state(false);
  let commentCount = $state(0);

  function togglePlay() {
    if (!videoEl) return;
    if (videoEl.paused) {
      videoEl?.play().catch(() => {});
    } else {
      videoEl.pause();
    }
  }

  $effect(() => {
    if (!videoEl) return;
    if (active) {
      videoEl.play().catch(() => {});
    } else {
      videoEl.pause();
    }
  });

  const isLongTitle = $derived((item.title?.length ?? 0) > TITLE_LIMIT);
  const displayedTitle = $derived(
    !item.title
      ? ""
      : expanded || !isLongTitle
        ? item.title
        : item.title.slice(0, TITLE_LIMIT).trimEnd(),
  );

  const isLong = $derived((item.description?.length ?? 0) > DESCRIPTION_LIMIT);
  const displayedDescription = $derived(
    !item.description
      ? ""
      : expanded || !isLong
        ? item.description
        : item.description.slice(0, DESCRIPTION_LIMIT).trimEnd(),
  );
  const canExpand = $derived(isLongTitle || isLong);

  const video = $derived(item.taskAttachments[0]);
</script>

<article
  class="relative h-full flex flex-col xl:flex-row xl:items-center xl:justify-center gap-0 xl:gap-3"
  inert={!active}
>
  <div
    class=" {showComments
      ? 'md:rounded-t-lg'
      : 'md:rounded-lg'} relative flex-1 min-h-0 md:flex-1 md:min-w-0 md:max-w-sm md:h-full xl:rounded-lg overflow-hidden bg-black shadow-sm transition"
  >
    <div class="absolute right-4 bottom-4 z-40 flex flex-col gap-2">
      <TaskReactions
        taskId={item.id}
        initialReactions={item.reactions ?? []}
        showReactions={false}
        {active}
        class="text-white hover:bg-[#242426] bg-[#242426]/30"
      />

      <div class="flex flex-col items-center gap-1">
        <button
          type="button"
          aria-label="Open comments"
          use:tooltip={showComments ? "Hide comments" : "Show comments"}
          class="z-10 bg-[#242426]/30 hover:bg-[#242426] icon-button
                {showComments
            ? 'bg-brand text-surface hover:bg-brand'
            : 'text-white'}"
          onclick={() => (showComments = !showComments)}
        >
          <CommentIcon />
        </button>
        <span class="text-sm text-white">
          {commentCount}
        </span>
      </div>

      <div class="flex flex-col items-center">
        <a
          href={resolve(`/tasks/${item.id}`)}
          class="icon-button bg-[#242426]/30 hover:bg-[#242426] text-white"
          aria-label="Go to task"
          use:tooltip={"Go to task"}
        >
          <UprightArrow size={1.5} />
        </a>
      </div>

      <div class="flex items-center">
        <ShareButton
          taskId={item.id}
          class="bg-[#242426]/30 hover:bg-[#242426] text-white"
          onClips={true}
        />
      </div>
    </div>

    {#if video}
      <video
        class="h-full w-full object-contain"
        src={taskAttachmentUrl(video.id)}
        bind:this={videoEl}
        {muted}
        playsinline
        onclick={togglePlay}
        onplay={() => (isPaused = false)}
        onpause={() => (isPaused = true)}
        loop
        preload="metadata"
      >
        <track kind="captions" />
      </video>

      {#if isPaused}
        <div
          class="absolute inset-0 flex items-center justify-center pointer-events-none"
        >
          <div class="bg-[#48484d]/80 rounded-full p-4 text-white">
            <PlayIcon size={1.5} />
          </div>
        </div>
      {/if}

      <button
        type="button"
        class="absolute top-4 right-4 icon-button bg-[#242426]/30 hover:bg-[#242426] text-white"
        onclick={() => (muted = !muted)}
        aria-label={muted ? "Unmute" : "Mute"}
        use:tooltip={muted ? "Unmute" : "Mute"}
      >
        <Speaker size={1.5} {muted} />
      </button>
    {/if}

    <div
      class="pointer-events-none absolute inset-x-0 bottom-0 p-3 flex flex-col gap-2 text-white"
    >
      <div
        class="pointer-events-auto flex items-center gap-3 w-fit {expanded
          ? 'bg-[#242426]/80'
          : 'bg-[#242426]/30'}  rounded-lg px-3 py-2"
      >
        <ProfileIcon
          size={2.25}
          userId={item.userId}
          alt={item.userDisplayName}
        />
        <div class="flex flex-col leading-tight">
          <div class="flex flex-wrap items-center leading-tight gap-1">
            <a
              href={resolve(`/profile/${item.userId}`)}
              class="font-semibold text-sm">{item.userDisplayName}</a
            >
            {#if item.selectedBadgeIcon}
              <span use:tooltip={item.selectedBadgeName || ""}>
                <BadgeIcon icon={item.selectedBadgeIcon} size={1.5} />
              </span>
            {/if}
          </div>
          <span class="text-xs text-white/70">{timeAgo(item.createdAt)}</span>
        </div>
      </div>
      <div
        class="{expanded
          ? 'bg-[#242426]/80'
          : 'bg-[#242426]/30'} rounded-lg gap-y-2 flex flex-col mr-16 py-2"
      >
        <button
          type="button"
          disabled={!canExpand}
          class:cursor-pointer={canExpand}
          class:cursor-default={!canExpand}
          class="pointer-events-auto px-4 text-left text-white wrap-anywhere disabled:cursor-default"
          onclick={() => (expanded = !expanded)}
        >
          <span class="font-serif font-semibold text-lg">
            {isLongTitle && !expanded ? displayedTitle + "..." : displayedTitle}
          </span>

          {#if item.description}
            <p
              class="mt-1 max-h-64 overflow-y-auto text-left text-sm wrap-anywhere text-white/90"
            >
              {displayedDescription}
              {#if canExpand && isLong && !expanded}
                <span
                  class="ml-0.5 text-xs font-medium text-white/60 align-baseline"
                >
                  more...
                </span>
              {/if}
            </p>
          {/if}
        </button>
      </div>
    </div>
  </div>

  <ClipCommentsPanel
    taskId={item.id}
    isOpen={showComments}
    {currentUserId}
    onClose={() => (showComments = false)}
    onCommentCountChange={(count) => (commentCount = count)}
  />
</article>
