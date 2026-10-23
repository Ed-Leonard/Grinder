<script lang="ts">
  import { page as routePage } from "$app/state";
  import { fetchExploreFeed } from "$lib/api/explore";
  import { search } from "$lib/api/search";
  import type { SearchResponse, Task } from "$lib/types";
  import TaskShowcase from "$lib/components/tasks/TaskShowcase.svelte";
  import SearchBar from "$lib/components/Explore/SearchBar.svelte";
  import UserResultCard from "$lib/components/Explore/UserResultCard.svelte";
  import ErrorPopup from "$lib/components/utils/Error.svelte";
  import type { Component } from "svelte";
  import GlobeIcon from "$lib/components/svgs/visibility/globe.svelte";
  import NotebookIcon from "$lib/components/svgs/badges/notebook.svelte";
  import FriendsIcon from "$lib/components/svgs/badges/friends-icon.svelte";
  import ClipIcon from "$lib/components/svgs/clips/clip.svelte";

  type SearchUser = NonNullable<SearchResponse["users"]>[number];

  type ExploreFilter = "all" | "tasks" | "clips" | "users";

  const FILTERS: {
    value: ExploreFilter;
    label: string;
    icon: Component<{ size?: number; colour?: string }>;
  }[] = [
    { value: "all", label: "All", icon: GlobeIcon },
    { value: "tasks", label: "Tasks", icon: NotebookIcon },
    { value: "clips", label: "Clips", icon: ClipIcon },
    { value: "users", label: "Users", icon: FriendsIcon },
  ];

  const MIN_QUERY_LENGTH = 2;
  const DEBOUNCE_MS = 300;

  // ── Query ──────────────────────────────────────────────────────────────
  // `searchQuery` is what's in the input. `query` is the normalized value the
  // data layer cares about: "" means "show the explore feed". Because it's a
  // $derived string, typing a trailing space or going from "a" to "" doesn't
  // change it, so nothing re-runs.
  let searchQuery = $state(routePage.url.searchParams.get("q") ?? "");
  const query = $derived(
    searchQuery.trim().length >= MIN_QUERY_LENGTH ? searchQuery.trim() : "",
  );
  const isSearching = $derived(query !== "");

  let activeFilter = $state<ExploreFilter>("all");

  const showUsers = $derived(
    activeFilter === "all" || activeFilter === "users",
  );
  const showTasks = $derived(
    activeFilter === "all" || activeFilter === "tasks",
  );
  const showClips = $derived(
    activeFilter === "all" || activeFilter === "clips",
  );

  // ── Pagination state ───────────────────────────────────────────────────
  let tasks = $state<Task[]>([]);
  let users = $state<SearchUser[]>([]);
  let page = $state(1);
  let totalPages = $state(1);
  let loading = $state(false);
  let debouncing = $state(true); // true until the first load is kicked off
  let error = $state("");

  let sentinel = $state<HTMLDivElement>();

  // Bumped whenever the query changes. Any in-flight request that finishes
  // under an older generation is thrown away, and can't touch state.
  let generation = 0;

  const busy = $derived(loading || debouncing);
  const hasMore = $derived(page <= totalPages);
  let reachedEnd = $state(false);

  const filteredTasks = $derived(tasks.filter((t) => !t.isClip));
  const filteredClips = $derived(tasks.filter((t) => t.isClip));

  const visibleUsers = $derived(showUsers ? users : []);
  const visibleTasks = $derived(showTasks ? filteredTasks : []);
  const visibleClips = $derived(showClips ? filteredClips : []);

  const isEmpty = $derived(tasks.length === 0 && users.length === 0);
  const hasVisibleResults = $derived(
    visibleUsers.length > 0 ||
      visibleTasks.length > 0 ||
      visibleClips.length > 0,
  );

  function reset() {
    generation++;
    tasks = [];
    users = [];
    page = 1;
    totalPages = 1;
    loading = false;
    reachedEnd = false;
    error = "";
  }

  async function loadNextPage() {
    if (loading || debouncing || reachedEnd || page > totalPages) return;

    const gen = generation;
    const q = query;
    loading = true;
    error = "";

    try {
      let added = 0;
      if (q) {
        const r = await search(q, page, 20);
        if (gen !== generation) return;
        const newUsers = r.users ?? [];
        const newTasks = r.tasks ?? [];
        users = [...users, ...newUsers];
        tasks = [...tasks, ...newTasks];
        totalPages = r.totalPages ?? 1;
        added = newUsers.length + newTasks.length;
      } else {
        const r = await fetchExploreFeed(page, 40);
        if (gen !== generation) return;
        tasks = [...tasks, ...r.items] as Task[];
        totalPages = r.totalPages;
        added = r.items.length;
      }
      page += 1;
      if (added === 0 || page > totalPages) reachedEnd = true;
    } catch (e: any) {
      if (gen === generation) error = e.message ?? "Failed to load";
    } finally {
      if (gen === generation) loading = false;
    }
  }

  // ── Query change → reset + (debounced) first page ─────────────────────
  // Runs on mount too, which replaces onMount/loadInitial. The first run
  // skips the debounce so a `?q=` URL loads immediately.
  let firstRun = true;
  $effect(() => {
    const q = query; // the only reactive dependency; everything below is untracked
    reset();
    debouncing = true;

    const delay = q && !firstRun ? DEBOUNCE_MS : 0;
    firstRun = false;

    const timer = setTimeout(() => {
      debouncing = false;
      loadNextPage();
    }, delay);

    return () => clearTimeout(timer);
  });

  // ── Infinite scroll ────────────────────────────────────────────────────
  // The observer is recreated every time we stop being busy. A new observer
  // fires once immediately with the current visibility, so if a page didn't
  // fill the screen the sentinel is still on screen and we load again. This
  // is what replaces checkAndLoadIfStillVisible / tick / notifyLoaded.
  //
  // `error` is in the guard so a failing request doesn't retry in a loop.
  $effect(() => {
    if (!sentinel || busy || !hasMore || reachedEnd || error) return;

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0].isIntersecting) loadNextPage();
      },
      { rootMargin: "200px" },
    );
    observer.observe(sentinel);
    return () => observer.disconnect();
  });
</script>

<div
  class="flex flex-col items-center flex-1 w-full gap-2 md:gap-4 min-h-full bg-surface-alt md:p-4"
>
  <div class="flex flex-col w-full items-center gap-4 p-2 md:p-0">
    <SearchBar bind:value={searchQuery} minLength={MIN_QUERY_LENGTH} />

    <div
      class="flex justify-center gap-1 w-full lg:w-xl"
      role="tablist"
      aria-label="Result filters"
    >
      {#each FILTERS as filter (filter.value)}
        {@const Icon = filter.icon}
        <button
          type="button"
          role="tab"
          aria-selected={activeFilter === filter.value}
          class="secondary-button inline-flex items-center justify-center gap-2 {activeFilter ===
          filter.value
            ? 'bg-brand text-surface'
            : 'text-text-muted'}"
          onclick={() => (activeFilter = filter.value)}
        >
          <Icon size={1.25} colour="currentColor" />
          <span class="hidden xs:block">
            {filter.label}
          </span>
        </button>
      {/each}
    </div>

    <hr class="w-full text-surface-alt-hover" />
  </div>

  <div class="flex flex-col md:gap-2 w-full items-center px-2">
    {#if error}
      <ErrorPopup {error} />
    {/if}

    {#if visibleUsers.length}
      <section class="w-full grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-2 md:gap-3">
        {#each visibleUsers as user (user.id)}
          <UserResultCard {user} />
        {/each}
      </section>
    {/if}

    {#if visibleUsers.length && (visibleTasks.length || visibleClips.length)}
      <hr class="w-full text-surface-alt-hover my-2" />
    {/if}

    {#if visibleTasks.length}
      <TaskShowcase tasks={visibleTasks} isExplorePage={!isSearching} />
    {/if}

    {#if visibleTasks.length && visibleClips.length}
      <hr class="w-full text-surface-alt-hover my-2" />
    {/if}

    {#if visibleClips.length}
      <TaskShowcase tasks={visibleClips} isExplorePage={!isSearching} />
    {/if}

    {#if !busy && !hasVisibleResults && (!isEmpty || isSearching)}
      <div class="text-text-muted mt-4 w-full text-center">
        {#if isSearching}
          No {activeFilter === "all" ? "results" : activeFilter} for "{query}".
        {:else}
          No {activeFilter === "all" ? "results" : activeFilter} to show.
        {/if}
      </div>
    {/if}
  </div>

  <div bind:this={sentinel} class="h-1 w-full" aria-hidden="true"></div>

  <div class="w-full flex items-center justify-center py-6 min-h-10">
    {#if busy}
      <div class="flex items-center gap-2 text-sm text-text-muted">
        <span
          class="w-4 h-4 rounded-full border-2 border-text-muted/30 border-t-text-muted animate-spin"
        ></span>
        <span>{isEmpty ? "Loading..." : "Loading more..."}</span>
      </div>
    {:else if error}
      <button
        type="button"
        class="text-sm underline text-text-muted"
        onclick={loadNextPage}
      >
        Try again
      </button>
    {:else if !hasMore || reachedEnd}
      <p class="text-text-muted">
        {#if hasVisibleResults}
          You have viewed all available content.
        {:else if isEmpty && !isSearching}
          Nothing to explore yet.
        {/if}
      </p>
    {/if}
  </div>
</div>
