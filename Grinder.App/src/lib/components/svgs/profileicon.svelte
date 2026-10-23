<script lang="ts">
  import { profilePictureUrl } from "$lib/api/media";
  import { resolve } from "$app/paths";
  import { getStatusCount } from "$lib/api/tasks";
  import { taskUpdateTick } from "$lib/stores/taskUpdate";

  let {
    size = 2,
    userId,
    tooltip = false,
    alt = "User profile",
    clickable = true,
  }: {
    size?: number;
    userId: number;
    tooltip?: boolean;
    alt?: string;
    clickable?: boolean;
  } = $props();

  let showTooltip = $state(false);

  let imageLoadFailed = $state(false);
  let statusCounts = $state<{
    todo: number;
    inProgress: number;
    done: number;
    overdue: number;
  } | null>(null);

  const colors = {
    done: "#10b981",
    inProgress: "#f4a261",
    overdue: "#f44336",
    todo: "#6c757d",
    empty: "#e0e0e0",
  };

  $effect(() => {
    userId;
    imageLoadFailed = false;
    statusCounts = null;
  });

  $effect(() => {
    const id = userId;
    $taskUpdateTick;
    let cancelled = false;

    getStatusCount(id).then((counts) => {
      if (!cancelled) statusCounts = counts;
    });
    return () => {
      cancelled = true;
    };
  });

  let ringGradient = $derived.by(() => {
    if (!statusCounts) return `conic-gradient(${colors.empty} 0 100%)`;

    const { todo, inProgress, done, overdue } = statusCounts;
    const total = todo + inProgress + done + overdue;

    if (total === 0) {
      return `conic-gradient(${colors.empty} 0 100%)`;
    }

    const segments = [
      { color: colors.todo, count: todo },
      { color: colors.inProgress, count: inProgress },
      { color: colors.done, count: done },
      { color: colors.overdue, count: overdue },
    ];

    let acc = 0;
    const stops = segments
      .filter((s) => s.count > 0)
      .map((s) => {
        const start = acc;
        acc += (s.count / total) * 100;
        return `${s.color} ${start}% ${acc}%`;
      });

    return `conic-gradient(${stops.join(", ")})`;
  });
</script>

{#snippet avatarContent()}
  <div
    style="width: {size}rem; height: {size}rem;"
    class="rounded-full relative flex shrink-0"
    role="presentation"
    onmouseenter={() => (showTooltip = true)}
    onmouseleave={() => (showTooltip = false)}
    onblur={() => (showTooltip = false)}
  >
    <div
      class="ring-border"
      style="background: {ringGradient}; --ring-thickness: {size < 4
        ? 2
        : 4}px;"
    >
      {#if !imageLoadFailed}
        <img
          src={profilePictureUrl(userId)}
          {alt}
          onerror={() => (imageLoadFailed = true)}
          class="w-full h-full rounded-full object-cover"
        />
      {:else}
        <div
          class="w-full h-full rounded-full bg-surface-alt flex items-center justify-center"
        >
          <svg
            xmlns="http://www.w3.org/2000/svg"
            class="w-full h-full text-text-muted"
            fill="none"
            viewBox="2.3 2.3 19.4 19.4"
            stroke="currentColor"
            stroke-width="1.5"
          >
            <path
              stroke-linecap="round"
              stroke-linejoin="round"
              d="M17.982 18.725A7.488 7.488 0 0012 15.75a7.488 7.488 0 00-5.982 2.975m11.963 0a9 9 0 10-11.963 0m11.963 0A8.966 8.966 0 0112 21a8.966 8.966 0 01-5.982-2.275M15 9.75a3 3 0 11-6 0 3 3 0 016 0z"
            />
          </svg>
        </div>
      {/if}
    </div>
    {#if tooltip && showTooltip}
      <div
        class="absolute -bottom-5/6 left-4 bg-surface border border-surface-alt shadow-lg p-4 rounded-lg z-50"
      >
        {#if statusCounts}
          <div class="flex flex-col">
            <div class="text-todo">
              {statusCounts.todo} To Do
            </div>
            <div class="text-inprogress">
              {statusCounts.inProgress} In Progress
            </div>
            <div class="text-done">
              {statusCounts.done} Done
            </div>
            <div class="text-overdue">
              {statusCounts.overdue} Overdue
            </div>
          </div>
        {/if}
      </div>
    {/if}
  </div>
{/snippet}

{#if clickable}
  <a
    href={resolve(`/profile/${userId}`)}
    aria-label="View user profile"
    class="inline-block rounded-full focus-visible:outline-2 focus-visible:outline-offset-2"
    style="width: {size}rem; height: {size}rem;"
  >
    {@render avatarContent()}
  </a>
{:else}
  <div style="width: {size}rem; height: {size}rem;">
    {@render avatarContent()}
  </div>
{/if}

<style>
  .ring-border {
    width: 100%;
    height: 100%;
    border-radius: 50%;
    padding: var(--ring-thickness, 4px);
    box-sizing: border-box;
  }
</style>
