<script lang="ts">
  import type { WrappedStatsResponse } from "$lib/api/wrapped";
  import { buildTopExtraStatSlides } from "$lib/api/extra-stats";

  interface Props {
    stats: WrappedStatsResponse;
  }

  let { stats }: Props = $props();

  let completionPercent = $derived(stats.completionPercent ?? 0);
  let topExtraStats = $derived(buildTopExtraStatSlides(stats.extraStats, 3));

  let hasSpotlight = $derived(!!stats.mostLikedTask || !!stats.mostLikedComment);
  let hasStatStrip = $derived(topExtraStats.length > 0);

  function completionPercentMessage(percent: number, started: number): string {
    if (started === 0) return "";
    if (percent === 100) return "Nothing left on the table.";
    if (percent >= 75) return "You cleared most of what you set out to do.";
    if (percent >= 25) return "A mixed bag, some finished, some still hanging around.";
    return "Big plans, small dent. There's always next year.";
  }

  let completionPercentMsg = $derived(
    completionPercentMessage(completionPercent, stats.tasksStarted),
  );
</script>

<div class="flex flex-col items-center pb-10 text-center text-surface">
  <h1 class="text-5xl md:text-7xl text-surface">That's a wrap!</h1>

  <p class="mt-6 max-w-lg text-lg md:text-xl">
    You completed {completionPercent}% of your tasks this year
  </p>
  {#if completionPercentMsg}
    <p class="mt-2 text-base opacity-80">{completionPercentMsg}</p>
  {/if}

  {#if hasSpotlight}
    <div
      class="mt-10 grid w-full max-w-2xl grid-cols-1 gap-x-10 gap-y-8 border-t border-surface/40 pt-10 text-left sm:grid-cols-2"
    >
      {#if stats.mostLikedTask}
        <div>
          <p class="text-sm opacity-80">Your most reacted-to task</p>
          <p class="mt-2 text-lg font-semibold">{stats.mostLikedTask.title}</p>
          <p class="mt-1 text-sm opacity-80">
            {stats.mostLikedTask.reactionCount} reaction{stats.mostLikedTask.reactionCount === 1
              ? ""
              : "s"}
          </p>
        </div>
      {/if}

      {#if stats.mostLikedComment}
        <div>
          <p class="text-sm opacity-80">Your most replied-to comment</p>
          <p class="mt-2 line-clamp-3 text-lg italic">"{stats.mostLikedComment.content}"</p>
          <p class="mt-1 text-sm opacity-80">
            {stats.mostLikedComment.replyCount} repl{stats.mostLikedComment.replyCount === 1
              ? "y"
              : "ies"}
          </p>
        </div>
      {/if}
    </div>
  {/if}

  {#if hasStatStrip}
    <div
      class="flex w-full max-w-2xl flex-wrap justify-center gap-x-12 gap-y-8 {hasSpotlight
        ? 'mt-10'
        : 'mt-14 border-t border-surface/40 pt-10'}"
    >
      {#each topExtraStats as stat (stat.id)}
        <div>
          <p class="font-serif text-4xl font-bold tabular-nums md:text-5xl">{stat.value}</p>
          <p class="mt-1 text-sm opacity-80">{stat.label}</p>
        </div>
      {/each}
    </div>
  {/if}

  <p class="mt-14 text-lg md:text-xl">Keep <span class="font-bold">Grinding!</span></p>
</div>