<script lang="ts">
  import { type Task } from "$lib/types";
  import { taskAttachmentUrl } from "$lib/api/media";
  import {
    getTileShape,
    TILE_SHAPES,
    type TileShape,
  } from "$lib/utils/aspectRatio";
  import { browser } from "$app/environment";
  import { resolve } from "$app/paths";
  import Pluscircle from "$lib/components/svgs/pluscircle.svelte";
  import { onMount } from "svelte";
  import { markSeen } from "$lib/feed/viewTracker";

  let {
    tasks,
    isOwnProfile = false,
    isExplorePage = false,
  }: {
    tasks: Task[];
    isOwnProfile?: boolean;
    isExplorePage?: boolean;
  } = $props();

  const GAP_PX = 0;

  let containerWidth = $state(0);
  let innerWidth = $state(browser ? window.innerWidth : 0);

  let gridColumns = $derived.by(() => {
    if (innerWidth < 1024) return 4;
    if (innerWidth < 1440) return 6;
    return 8;
  });

  let colWidth = $derived(
    containerWidth > 0
      ? (containerWidth - GAP_PX * (gridColumns - 1)) / gridColumns
      : 0,
  );

  function hashId(id: number | string): number {
    const str = String(id);
    let h = 2166136261;
    for (let i = 0; i < str.length; i++) {
      h ^= str.charCodeAt(i);
      h = Math.imul(h, 16777619);
    }
    return Math.abs(h);
  }

  const NO_MEDIA_SHAPES: TileShape[] = ["square", "wide"];

  export function getNoMediaVariant(taskId: number | string): {
    shape: TileShape;
    gradientAngle: number;
    gradientFlip: boolean;
  } {
    const h = hashId(taskId);

    const shape = NO_MEDIA_SHAPES[h % NO_MEDIA_SHAPES.length];

    const gradientAngle = 40 + (h % 5) * 15;

    const gradientFlip = (h >> 3) % 2 === 0;

    return { shape, gradientAngle, gradientFlip };
  }

  interface TileRenderInfo {
    hero: Task["taskAttachments"][number] | undefined;
    span: { colSpan: number; rowSpan: number };
    mismatch: number;
    noMediaVariant: ReturnType<typeof getNoMediaVariant> | null;
  }

  function getTileRenderInfo(task: Task, index: number): TileRenderInfo {
    const hero = task.taskAttachments?.[0];

    if (!hero) {
      const noMediaVariant = getNoMediaVariant(task.id);
      return {
        hero: undefined,
        span: TILE_SHAPES[noMediaVariant.shape],
        mismatch: 0,
        noMediaVariant,
      };
    }

    const shape = getTileShape(hero.width, hero.height, index % 4 == 0);
    const span = TILE_SHAPES[shape];
    const ratio = hero.width && hero.height ? hero.width / hero.height : 1;
    const shapeRatio = span.colSpan / span.rowSpan;
    const mismatch = Math.abs(Math.log(ratio / shapeRatio));

    return { hero, span, mismatch, noMediaVariant: null };
  }

  let taskEls = new Map();
  let seenIds = new Set();

  function registerCard(node: HTMLElement, taskId: number) {
    taskEls.set(taskId, node);
    observer?.observe(node);

    return {
      destroy() {
        taskEls.delete(taskId);
        observer?.unobserve(node);
      },
    };
  }

  let observer: IntersectionObserver;

  onMount(() => {
    if (!isExplorePage) return;

    observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          if (!entry.isIntersecting) continue;

          // find task id for this element
          for (const [id, el] of taskEls) {
            if (el === entry.target && !seenIds.has(id)) {
              seenIds.add(id);
              markSeen(id);
              observer.unobserve(el);
              break;
            }
          }
        }
      },
      { threshold: 0.6 },
    );

    taskEls.forEach((el) => observer.observe(el));

    return () => observer.disconnect();
  });
</script>

<svelte:window bind:innerWidth />
<div
  class="grid w-full overflow-hidden"
  style="
    grid-template-columns: repeat({gridColumns}, 1fr);
    grid-auto-rows: {colWidth}px;
    gap: {GAP_PX}px;
    grid-auto-flow: dense;
  "
  bind:clientWidth={containerWidth}
>
  {#if colWidth > 0}
    {#each tasks as task (task.id)}
      {@const info = getTileRenderInfo(task, tasks.indexOf(task))}
      <a
        class="group relative flex flex-col overflow-hidden"
        style="grid-column: span {info.span.colSpan}; grid-row: span {info.span
          .rowSpan};"
        href={resolve(`/tasks/${task.id}`)}
        use:registerCard={task.id}
        onmouseenter={(e) => {
          e.currentTarget.querySelectorAll("video").forEach((v) => v.play());
        }}
        onmouseleave={(e) => {
          e.currentTarget.querySelectorAll("video").forEach((v) => v.pause());
        }}
      >
        {#if info.hero}
          {@const hero = info.hero}

          <div class="inset-0 overflow-hidden absolute">
            {#if hero.contentType?.startsWith("video/")}
              {#if info.mismatch > 0.2}
                <video
                  src={taskAttachmentUrl(hero.id)}
                  muted
                  playsinline
                  loop
                  preload="metadata"
                  aria-hidden="true"
                  class="absolute inset-0 h-full w-full object-cover blur-lg scale-120"
                ></video>
                <video
                  src={taskAttachmentUrl(hero.id)}
                  muted
                  playsinline
                  loop
                  preload="metadata"
                  class="h-full w-full object-contain relative"
                ></video>
              {:else}
                <video
                  src={taskAttachmentUrl(hero.id)}
                  muted
                  playsinline
                  loop
                  preload="metadata"
                  class="h-full w-full object-cover"
                ></video>
              {/if}
            {:else if info.mismatch > 0.2}
              <img
                src={taskAttachmentUrl(hero.id)}
                alt=""
                aria-hidden="true"
                class="absolute inset-0 h-full w-full object-cover blur-lg scale-125"
              />
              <img
                src={taskAttachmentUrl(hero.id)}
                alt={hero.originalFileName}
                loading="lazy"
                class="h-full w-full object-contain relative"
              />
            {:else}
              <img
                src={taskAttachmentUrl(hero.id)}
                alt={hero.originalFileName}
                loading="lazy"
                class="h-full w-full object-cover"
              />
            {/if}
          </div>
          <div
            class="absolute inset-0 inset-ring inset-ring-surface-alt-hover group-hover:inset-ring-4 transition-shadow"
          ></div>
        {:else if info.noMediaVariant}
          {@const variant = info.noMediaVariant}
          <div
            class="flex h-full inset-ring inset-ring-surface-alt-hover hover:inset-ring-4 justify-center items-center transition-shadow p-2 md:p-4 overflow-hidden"
            style="
          background: linear-gradient({variant.gradientAngle}deg, var(--color-surface) 0%, var(--color-surface-alt-hover) 47%,
          var(--color-surface-alt-hover) 53%, var(--color-surface) 100%);
          background-size: 150% 150%;
          animation: gradientShift 4s ease-in-out infinite;
          {variant.gradientFlip ? 'transform: scaleX(-1);' : ''}
        "
          >
            <h5
              class="wrap-anywhere xs:text-lg sm:text-xl lg:text-2xl line-clamp-2"
              style={variant.gradientFlip ? "transform: scaleX(-1);" : ""}
            >
              {task.title}
            </h5>
          </div>
        {/if}
      </a>
    {/each}
  {/if}
</div>
{#if tasks.length === 0}
  <div class="text-text-muted mt-4 items-center w-full flex flex-col gap-4">
    <div>
      No tasks to see here. {isOwnProfile ? "Create one to get started!" : ""}
    </div>
    {#if isOwnProfile}
      <a
        class="primary-button inline-flex gap-2 items-center"
        href={resolve("/tasks/new")}
      >
        <Pluscircle /> Create Task
      </a>
    {/if}
  </div>
{/if}
