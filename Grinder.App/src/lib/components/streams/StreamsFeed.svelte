<script lang="ts">
  import { onMount } from "svelte";
  import { resolve } from "$app/paths";
  import { fetchLiveStreams, type LiveStreamItem } from "$lib/api/livestreams";
  import LivesCard from "$lib/components/streams/StreamsCard.svelte";
  import ErrorPopup from "$lib/components/utils/Error.svelte";

  let items = $state<LiveStreamItem[]>([]);
  let hasMore = $state(true);
  let loading = $state(false);
  let showLoadingIndicator = $state(false);
  let hasLoadedOnce = $state(false);
  let error = $state("");

  let sentinel = $state<HTMLDivElement>();
  let activeIndex = $state(0);
  let sectionEls = $state<(HTMLElement | undefined)[]>([]);
  let muted = $state(true);

  let reachedEnd = $derived(!loading && items.length > 0 && !hasMore);
  let totalSections = $derived(items.length + (reachedEnd ? 1 : 0));

  let sectionObserver: IntersectionObserver | undefined;
  const indexByNode = new Map<Element, number>();

  function trackActive(node: HTMLElement, index: number) {
    indexByNode.set(node, index);
    if (!sectionObserver) {
      sectionObserver = new IntersectionObserver(
        (entries) => {
          for (const entry of entries) {
            if (entry.isIntersecting) {
              const idx = indexByNode.get(entry.target);
              if (idx !== undefined) activeIndex = idx;
            }
          }
        },
        { threshold: 0.6 },
      );
    }
    sectionObserver.observe(node);
    return {
      destroy() {
        sectionObserver?.unobserve(node);
        indexByNode.delete(node);
      },
    };
  }

  async function loadNextPage() {
    if (loading || !hasMore) return;
    loading = true;
    error = "";

    const timer = setTimeout(() => {
      if (loading) showLoadingIndicator = true;
    }, 150);

    try {
      const seenRoomNames = items.map((i) => i.roomName);
      const result = await fetchLiveStreams(20, seenRoomNames);
      items = [...items, ...result.items];
      hasMore = result.hasMore;
    } catch (e: any) {
      error = e.message ?? "Failed to load live streams";
    } finally {
      clearTimeout(timer);
      loading = false;
      showLoadingIndicator = false;
      hasLoadedOnce = true;
    }
  }

  function handleKeydown(e: KeyboardEvent) {
    if (e.key !== "ArrowDown" && e.key !== "ArrowUp") return;

    const target = document.activeElement;
    if (
      target instanceof HTMLElement &&
      ["INPUT", "TEXTAREA"].includes(target.tagName)
    ) {
      return;
    }

    e.preventDefault();
    const nextIndex =
      e.key === "ArrowDown"
        ? Math.min(activeIndex + 1, totalSections - 1)
        : Math.max(activeIndex - 1, 0);

    sectionEls[nextIndex]?.scrollIntoView({
      behavior: "smooth",
      block: "start",
    });
  }

  onMount(() => {
    loadNextPage();

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0].isIntersecting) loadNextPage();
      },
      { rootMargin: "200px" },
    );

    if (sentinel) observer.observe(sentinel);
    window.addEventListener("keydown", handleKeydown);

    return () => {
      observer.disconnect();
      sectionObserver?.disconnect();
      window.removeEventListener("keydown", handleKeydown);
    };
  });
</script>

<div class="h-full w-full overflow-y-auto snap-y snap-mandatory bg-surface-alt">
  {#if error}
    <div class="p-4">
      <ErrorPopup {error} />
    </div>
  {/if}

  {#each items as item, i (item.id)}
    <section
      class="h-full w-full snap-start snap-always flex items-center justify-center md:p-8"
      bind:this={sectionEls[i]}
      use:trackActive={i}
    >
      <LivesCard
        {item}
        active={i === activeIndex}
        preload={i === activeIndex + 1}
        bind:muted
      />
    </section>
  {/each}

  {#if !loading && items.length === 0 && hasLoadedOnce}
    <p class="text-center text-text-muted py-8">
      No one's live right now - follow some people or start your own stream!
    </p>
  {/if}

  {#if reachedEnd}
    <section
      class="h-full w-full snap-start snap-always flex items-center justify-center p-4 md:p-8"
      bind:this={sectionEls[items.length]}
      use:trackActive={items.length}
    >
      <div
        class="max-w-md w-full mx-auto rounded-lg border border-surface-alt bg-surface p-8 text-center"
      >
        <p class="text-lg font-serif font-semibold text-brand">
          You're all caught up
        </p>
        <p class="mt-1 text-sm text-text-muted">
          Start a productivity trend - go live and show your friends how it's
          done.
        </p>
        <a
          href={resolve("/livestream/start")}
          class="primary-button mt-4 inline-block"
        >
          Start a live stream
        </a>
      </div>
    </section>
  {/if}

  {#if showLoadingIndicator}
    <div class="flex justify-center py-4">
      <div class="spinner-border text-primary" role="status">
        <span class="visually-hidden">Loading...</span>
      </div>
    </div>
  {/if}

  <div bind:this={sentinel} class="h-1"></div>
</div>

