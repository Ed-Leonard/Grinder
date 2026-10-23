<script lang="ts">
  import XIcon from "$lib/components/svgs/xicon.svelte";
  import { tooltip } from "$lib/utils/tooltip";
  import LeftArrow from "$lib/components/svgs/leftarrow.svelte";
  import RightArrow from "$lib/components/svgs/rightarrow.svelte";
  import { fetchWrappedStats, type WrappedStatsResponse } from "$lib/api/wrapped";
  import { buildTopExtraStatSlides, type ExtraStatSlideData } from "../../api/extra-stats";
  import ExtraStatSlide from "./WrappedExtraStats.svelte";
  import WrappedSummary from "$lib/components/Wrapped/WrappedSummary.svelte";

  interface Props {
    open?: boolean;
    year: number;
    onclose?: () => void;
  }

  let { open = $bindable(false), year, onclose }: Props = $props();

  type Slide = {
    key: string;
    title: string;
    closing?: boolean;
    extraStat?: ExtraStatSlideData;
  };

  const leadingSlides: Slide[] = [
    { key: "total-tasks", title: "Tasks started and completed percentages" },
    { key: "most-liked", title: "Most liked task and comment" },
    { key: "top-categories", title: "Top 5 categories" },
  ];
  const closingSlide: Slide = { key: "closing", title: "End slide", closing: true };

  const SLIDE_DURATION = 6000; //Time per slide
  const HOLD_THRESHOLD = 200; //Length of time to distinguish between tap/click and hold

  let current = $state(0);
  let progress = $state(0);
  let paused = $state(false);

  let stats = $state<WrappedStatsResponse | null>(null);

  // Slide count depends on how many non-zero stats this user has (up to 3),
  // so it's only known once `stats` has loaded.
  let extraStatSlides = $derived(buildTopExtraStatSlides(stats?.extraStats));
  let slides = $derived<Slide[]>([
    ...leadingSlides,
    ...extraStatSlides.map((s) => ({ key: `extra:${s.id}`, title: s.label, extraStat: s })),
    closingSlide,
  ]);
  let lastIndex = $derived(slides.length - 1);

  let activeSlide = $derived(slides[current]);
  let isClosing = $derived(!!activeSlide?.closing);

  // requestAnimationFrame-based timer so we can pause by freezing elapsed time.
  let rafId: number | null = null;
  let slideStart = 0;
  let elapsedBeforePause = 0;
  let holdTimer: ReturnType<typeof setTimeout> | null = null;
  let didHold = false;

  function completionComment(started: number, percent: number): string {
    if (started === 0) return "No tasks this year";
    if (percent === 100) return "Every task you started, you finished.";
    if (percent >= 75) return "You were pretty good this year.";
    if (percent >= 25) return "You did ok.";
    return "Lots of ideas, zero execution....";
  }

  let lastAction = 0;
  const DEBOUNCE_TIME = 100;

  function clearRaf() {
    if (rafId !== null) {
      cancelAnimationFrame(rafId);
      rafId = null;
    }
  }

  function tick(now: number) {
    if (paused) {
      return;
    }
    const elapsed = elapsedBeforePause + (now - slideStart);
    progress = Math.min(elapsed / SLIDE_DURATION, 1);
    if (progress >= 1) {
      next();
      return;
    }
    rafId = requestAnimationFrame(tick);
  }

  function startTimer(index: number) {
    clearRaf();
    if (slides[index]?.closing) {
      progress = 1;
      return;
    }
    progress = 0;
    elapsedBeforePause = 0;
    slideStart = performance.now();
    rafId = requestAnimationFrame(tick);
  }

  function pause() {
    if (paused || isClosing) {
      return;
    }
    paused = true;
    elapsedBeforePause += performance.now() - slideStart;
    clearRaf();
  }

  function resume() {
    if (!paused || isClosing) return;
    paused = false;
    slideStart = performance.now();
    rafId = requestAnimationFrame(tick);
  }

  function goTo(index: number) {
    //Debounce rapid clicks/taps to avoid skipping slides.
    const now = performance.now();
    if (now - lastAction < DEBOUNCE_TIME) {
      return;
    }
    lastAction = now;
    //Bound the slide, first slide no going backwards, final slide no going forwards.
    const target = Math.max(0, Math.min(index, lastIndex));
    current = target;
    paused = false;
    startTimer(target);
  }

  function next() {
    //No forward wrapping stop on the closing slide.
    if (current >= lastIndex) return;
    goTo(current + 1);
  }

  function prev() {
    //Go back a slide, on the first slide it replays from the start.
    goTo(current - 1);
  }

  function close() {
    clearRaf();
    open = false;
    onclose?.();
  }

  function handlePointerDown() {
    didHold = false;
    holdTimer = setTimeout(() => {
      didHold = true;
      pause();
    }, HOLD_THRESHOLD);
  }

  function handlePointerUp(direction: "prev" | "next") {
    if (holdTimer) {
      clearTimeout(holdTimer);
      holdTimer = null;
    }
    if (didHold) {
      //Was a hold so just resume playback, don't navigate.
      resume();
      return;
    }
    if (direction === "next") next();
    else prev();
  }

  function handleKeydown(e: KeyboardEvent) {
    if (!open) return;
    if (e.key === "Escape") close();
  }

  //Reset to the first slide and restart the timer whenever the modal opens.
  $effect(() => {
    if (!open) {
      clearRaf();
      return;
    }

    current = 0;
    paused = false;
    stats = null;
    let cancelled = false;

    fetchWrappedStats(year).then((data) => {
      if (cancelled) return;
      stats = data;
      startTimer(0);
    });

    return () => {
      cancelled = true;
    };
  });
</script>

<svelte:window onkeydown={handleKeydown} />

{#if open}
  <div
    class="fixed inset-0 z-50 flex flex-col overflow-hidden text-surface bg-brand"
    role="dialog"
    aria-modal="true"
    aria-label="Grinder Wrapped"
  >
    <div class="relative z-20">
      <div class="flex gap-2 px-4 pt-4 md:px-8">
        {#each slides as slide, i (slide.key)}
          <div class="h-1 flex-1 overflow-hidden rounded-full bg-white/30">
            <div
              class="h-full rounded-full bg-white"
              style="width: {i < current ? 100 : i === current ? progress * 100 : 0}%"
            ></div>
          </div>
        {/each}
      </div>

      <div class="flex items-center justify-end px-4 pt-4 md:px-8">
        <button
          class="icon-button text-surface hover:bg-text"
          onclick={close}
          aria-label="Close Wrapped"
          use:tooltip={"Close Wrapped"}
        >
          <XIcon size={1.5} />
        </button>
      </div>
    </div>

    <div class="flex min-h-0 flex-1 select-none items-start justify-center overflow-y-auto px-16 md:px-20 sm:items-center">
      <div
        class="w-full max-w-3xl"
      >
        {#if !stats}
          <h1 class="text-surface">Loading your Wrapped…</h1>
        {:else if activeSlide.key === "total-tasks"}
          <p class="text-sm font-semibold uppercase tracking-[0.2em]">This year</p>
          <div class="mt-10 grid grid-cols-1 gap-10 md:grid-cols-2 md:gap-16">
            <div class="md:text-left">
              <h1 class="text-6xl md:text-left md:text-8xl text-surface">{stats.tasksStarted}</h1>
              <p class="mt-2 text-lg">tasks started</p>
            </div>
            <div class="md:text-left">
              <h1 class="text-6xl md:text-left md:text-8xl text-surface">{stats.tasksCompleted}</h1>
              <p class="mt-2 text-lg">completed</p>
            </div>
          </div>
          <p class="mt-12 text-lg md:text-xl">
            {stats.completionPercent}% - {completionComment(
              stats.tasksStarted,
              stats.completionPercent,
            )}
          </p>
        {:else if activeSlide.key === "most-liked"}
          <p class="text-sm font-semibold uppercase tracking-[0.2em]">Most reacted to task</p>
          {#if stats.mostLikedTask}
            <h1 class="mt-6 md:text-left text-surface">{stats.mostLikedTask.title}</h1>
            <p class="mt-3 text-lg">{stats.mostLikedTask.reactionCount} reactions</p>
          {:else}
            <h2 class="mt-6">No reactions on your tasks this year.</h2>
          {/if}
          <div class="mt-14 border-t border-surface pt-10">
            <p class="text-sm font-semibold uppercase tracking-[0.2em]">
              Most replied to comment
            </p>
            {#if stats.mostLikedComment}
              <h2 class="mt-5 font-normal italic md:text-left text-surface">
                “{stats.mostLikedComment.content}”
              </h2>
              <p class="mt-3 text-lg">{stats.mostLikedComment.replyCount} replies</p>
            {:else}
              <p class="mt-5 text-lg">No comments with replies this year.</p>
            {/if}
          </div>
        {:else if activeSlide.key === "top-categories"}
          <h1 class="md:text-lef text-surface">Top categories</h1>
          {#if !stats.topCategories || stats.topCategories.length === 0}
            <p class="mt-6 text-lg">No categories this year.</p>
          {:else}
            <ol class="mt-10 space-y-5">
              {#each stats.topCategories as category, i}
                <li class="flex items-baseline gap-5">
                  <span class="w-10 shrink-0 font-serif text-3xl font-bold tracking-tight"
                    >{i + 1}</span
                  >
                  <h2 class="min-w-0 flex-1 text-left text-surface">{category.name}</h2>
                  <span class="shrink-0 text-lg">{category.taskCount}</span>
                </li>
              {/each}
            </ol>
          {/if}
        {:else if activeSlide.extraStat}
          <ExtraStatSlide
            rank={activeSlide.extraStat.rank}
            label={activeSlide.extraStat.label}
            value={activeSlide.extraStat.value}
            message={activeSlide.extraStat.message}
            rateLine={activeSlide.extraStat.rateLine}
            otherHighlights={activeSlide.extraStat.otherHighlights}
          />
        {:else if activeSlide.key === "closing"}
          <WrappedSummary {stats} />
        {:else}
          <h1 class="text-surface">{activeSlide.title}</h1>
        {/if}
      </div>
    </div>
    {#if current > 0}
      <div
        class="pointer-events-none absolute left-4 top-1/2 z-20 -translate-y-1/2md:left-8"
        aria-hidden="true"
        use:tooltip={"Previous slide"}
      >
        <LeftArrow size={2} />
      </div>
    {/if}
    {#if !isClosing}
      <div
        class="pointer-events-none absolute right-4 top-1/2 z-20 -translate-y-1/2  md:right-8"
        aria-hidden="true"
        use:tooltip={"Next slide"}
      >
        <RightArrow size={2} />
      </div>
    {/if}
    <button
      type="button"
      class="absolute bottom-0 left-0 z-10 w-1/2 cursor-default"
      style="top: 5rem;"
      aria-label="Previous slide"
      onpointerdown={handlePointerDown}
      onpointerup={() => handlePointerUp("prev")}
    ></button>
    {#if !isClosing}
      <button
        type="button"
        class="absolute bottom-0 right-0 z-10 w-1/2 cursor-default"
        style="top: 5rem;"
        aria-label="Next slide"
        onpointerdown={handlePointerDown}
        onpointerup={() => handlePointerUp("next")}
      ></button>
    {/if}
  </div>
{/if}

<style>
  @keyframes wrappedGradientShift {
    0%,
    100% {
      background-position: 0% 50%;
    }
    50% {
      background-position: 100% 50%;
    }
  }
</style>