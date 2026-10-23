<script lang="ts">
  import { replaceState } from "$app/navigation";
  import { page } from "$app/state";
  import SearchIcon from "$lib/components/svgs/search.svelte";

  let {
    value = $bindable(""),
    minLength = 2,
    placeholder = "Search for friends, posts, or categories...",
  }: {
    value?: string;
    minLength?: number;
    placeholder?: string;
  } = $props();

  // Mirror the query into ?q= so the URL is shareable and survives a refresh.
  // This runs from the input event (not an $effect), so it never fires on
  // mount, and replaceState (shallow routing) updates the address bar without
  // triggering a navigation or re-running any `load` functions.
  function syncUrl(raw: string) {
    const trimmed = raw.trim();
    const q = trimmed.length >= minLength ? trimmed : "";
    if ((page.url.searchParams.get("q") ?? "") === q) return;

    const url = new URL(page.url);
    if (q) url.searchParams.set("q", q);
    else url.searchParams.delete("q");

    replaceState(url, page.state);
  }
</script>

<div class="relative w-full lg:w-xl border-surface-alt">
  <div class="absolute left-2 top-1/2 -translate-y-1/2 w-4 h-4">
    <SearchIcon />
  </div>
  <input
    type="text"
    {placeholder}
    class="border-surface-alt-hover pl-8 search-field"
    bind:value
    oninput={(e) => syncUrl(e.currentTarget.value)}
  />
</div>
