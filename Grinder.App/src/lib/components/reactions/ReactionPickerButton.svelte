<script lang="ts">
  import { fetchReactions, type Reaction } from "$lib/api/reaction";
  import { tick } from "svelte";
  import FaceIcon from "$lib/components/svgs/face.svelte";
  import Popover from "$lib/components/Popover.svelte";
  import { resolve } from "$app/paths";
  import { tooltip } from "$lib/utils/tooltip";

  let {
    selectedNames,
    onPick,
    class: className = "",
    showReactions = true,
  }: {
    selectedNames?: string[];
    onPick?: (reaction: Reaction) => void;
    class?: string;
    showReactions?: boolean;
  } = $props();

  let allReactions = $state<Reaction[]>([]);
  let query = $state("");
  let searchInput = $state<HTMLInputElement | null>(null);

  let filteredReactions = $derived(
    query.trim() === ""
      ? allReactions
      : allReactions.filter((r) =>
          r.name.toLowerCase().includes(query.trim().toLowerCase()),
        ),
  );

  async function loadReactions() {
    try {
      allReactions = await fetchReactions();
    } catch (error) {
      console.error("Failed to load reactions:", error);
    }
  }

  function isTouchDevice() {
    return (
      typeof window !== "undefined" &&
      window.matchMedia("(pointer: coarse)").matches
    );
  }

  function handleOpen() {
    query = "";
    if (allReactions.length === 0) loadReactions();
    // prevent keyboard pop up on mobile because it blocks the pop up itself usually
    if (!isTouchDevice()) {
      tick().then(() => searchInput?.focus());
    }
  }

  function pick(reaction: Reaction, close: () => void) {
    onPick?.(reaction);
    close();
  }

  function isSelected(name: string) {
    return selectedNames ? selectedNames.includes(name) : null;
  }
</script>

<Popover
  onopen={handleOpen}
  vertical={showReactions ? "auto" : "up"}
  horizontal={showReactions ? "auto" : "left"}
  panelClass="flex h-64 w-64 flex-col"
>
  {#snippet trigger({ open, toggle })}
    <button
      type="button"
      aria-label="Add reaction"
      aria-expanded={open}
      use:tooltip={"Add reaction"}
      onclick={toggle}
      class="icon-button {open ? 'bg-brand text-surface' : className}"
    >
      <FaceIcon />
    </button>
  {/snippet}

  {#snippet content({ close })}
    <div class="border-b border-surface-alt p-2">
      <input
        bind:this={searchInput}
        bind:value={query}
        type="text"
        placeholder="Search reactions..."
        class="search-field"
      />
    </div>

    <div class="flex-1 overflow-y-auto p-2">
      {#if filteredReactions.length > 0}
        <div class="grid grid-cols-5 gap-0.5" role="listbox">
          {#each filteredReactions as reaction (reaction.name)}
            <button
              type="button"
              use:tooltip={`:${reaction.name}:`}
              onclick={() => pick(reaction, close)}
              class="flex aspect-square items-center justify-center rounded-lg border cursor-pointer
                {isSelected(reaction.name)
                ? 'border-brand bg-surface-alt'
                : 'border-transparent hover:bg-surface-hover'}"
            >
              <img
                src={resolve(`/reactions/${reaction.fileName}` as any)}
                alt={reaction.name}
                loading="lazy"
                class="pointer-events-none h-[30px] w-[30px] object-contain"
              />
            </button>
          {/each}
        </div>
      {:else}
        <p class="p-2 text-center text-sm text-text-muted">
          No reactions found
        </p>
      {/if}
    </div>
  {/snippet}
</Popover>
