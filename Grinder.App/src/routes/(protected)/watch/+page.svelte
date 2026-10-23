<script lang="ts">
  import { page } from "$app/stores";
  import { goto } from "$app/navigation";
  import ViewSwitcher from "$lib/components/clips/ViewSwitcher.svelte";
  import ClipsFeed from "$lib/components/clips/ClipsFeed.svelte";
  import StreamsFeed from "$lib/components/streams/StreamsFeed.svelte";
  import ClipIcon from "$lib/components/svgs/clips/clip.svelte";
  import StreamIcon from "$lib/components/svgs/broadcast.svelte";

  const VIEW_OPTIONS = [
    { value: "Clips", label: "Clips", icon: ClipIcon },
    { value: "Streams", label: "Streams", icon: StreamIcon },
  ];

  let view = $derived($page.url.searchParams.get("view") === "streams" ? "Streams" : "Clips");

  function setView(next: string) {
    const url = new URL($page.url);
    if (next === "Streams") {
      url.searchParams.set("view", "streams");
    } else {
      url.searchParams.delete("view");
    }
    goto(url, { replaceState: true, noScroll: true, keepFocus: true });
  }
</script>

<div class="flex flex-col h-full w-full min-h-0">
  <div class="bg-surface-alt">
    <ViewSwitcher active={view} options={VIEW_OPTIONS} onSelect={setView} />
  </div>

  <hr class="text-surface-alt-hover" />

  <div class="flex-1 min-h-0">
    {#if view === "Streams"}
      <StreamsFeed />
    {:else}
      <ClipsFeed />
    {/if}
  </div>
</div>