<script lang="ts">
  import { onMount } from "svelte";
  import { fetchFeed, type FeedItem } from "$lib/api/feed";
  import FeedCard from "$lib/components/feed/FeedCard.svelte";
  import ErrorPopup from "$lib/components/utils/Error.svelte";

  let items = $state<FeedItem[]>([]);
  let page = $state(1);
  let totalPages = $state(1);
  let loading = $state(false);
  let error = $state("");

  let sentinel = $state<HTMLDivElement>();

  async function loadNextPage() {
    if (loading || page > totalPages) return;
    loading = true;
    error = "";
    try {
    const result = await fetchFeed(page, 20);
    const newItems = result.items.filter(item => !items.some(existing => existing.id === item.id));
    items = [...items, ...newItems];
      totalPages = result.totalPages;
      page += 1;
    } catch (e: any) {
      error = e.message ?? "Failed to load feed";
    } finally {
      loading = false;
    }
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
    return () => observer.disconnect();
  });
</script>

<div class="min-h-screen bg-surface-alt md:p-8">
  <div class="flex flex-col md:gap-4 w-full max-w-4xl mx-auto">
    {#if error}
      <ErrorPopup {error} />
    {/if}

    {#each items as item (item.id)}
      <FeedCard {item} />
    {/each}

    {#if !loading && items.length === 0 && page > 1}
      <p class="text-center text-text-muted py-8">
        No posts from friends yet - go follow some people!
      </p>
    {/if}

    {#if loading}
      <div class="flex justify-center py-4">
        <div class="spinner-border text-primary" role="status">
          <span class="visually-hidden">Loading...</span>
        </div>
      </div>
    {/if}

    <div bind:this={sentinel} class="h-1"></div>
  </div>
</div>
