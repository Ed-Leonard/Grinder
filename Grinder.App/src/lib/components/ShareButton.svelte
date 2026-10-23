<script lang="ts">
  import Message from "./svgs/message.svelte";
  import { tooltip } from "$lib/utils/tooltip";
  import Popover from "$lib/components/Popover.svelte";
  import { tick } from "svelte";
  import {
    getShareableFriendsForTask,
    shareTask,
  } from "$lib/api/directMessages";
  import type { Friend } from "$lib/api/follows";
  import Profileicon from "./svgs/profileicon.svelte";
  import { SvelteSet } from "svelte/reactivity";

  let {
    taskId,
    onClips = false,
    class: className = "",
  }: {
    taskId: number;
    class?: string;
    onClips?: boolean;
  } = $props();

  let query = $state("");
  let searchInput = $state<HTMLInputElement | null>(null);
  let sharing = $state(false);
  let error = $state<string | null>(null);

  let friends: Friend[] = $state<Friend[]>([]);
  let filteredFriends = $derived(
    query.trim() === ""
      ? friends
      : friends.filter((r) =>
          r.displayName.toLowerCase().includes(query.trim().toLowerCase()),
        ),
  );

  const selectedFriendIds = new SvelteSet<number>();

  function isTouchDevice() {
    return (
      typeof window !== "undefined" &&
      window.matchMedia("(pointer: coarse)").matches
    );
  }

  function handleOpen() {
    query = "";
    error = null;
    selectedFriendIds.clear();

    getShareableFriendsForTask(taskId)
      .then((result) => {
        friends = result;
      })
      .catch((err) => {
        console.error("Failed to load shareable friends:", err);
        error = "Couldn't load friends to share with.";
      });

    if (!isTouchDevice()) {
      tick().then(() => searchInput?.focus());
    }
  }

  function toggleFriend(id: number) {
    if (selectedFriendIds.has(id)) selectedFriendIds.delete(id);
    else selectedFriendIds.add(id);
  }

  async function share(close: () => void) {
    if (selectedFriendIds.size === 0 || sharing) return;

    sharing = true;
    error = null;
    try {
      await shareTask(taskId, [...selectedFriendIds]);
      close();
    } catch (err) {
      console.error("Failed to share task:", err);
      error = err instanceof Error ? err.message : "Failed to share task.";
    } finally {
      sharing = false;
    }
  }
</script>

<Popover
  onopen={handleOpen}
  vertical={!onClips ? "auto" : "up"}
  horizontal={!onClips ? "auto" : "left"}
  panelClass="flex h-72 w-72 flex-col"
>
  {#snippet trigger({ open, toggle })}
    <button
      class="icon-button {className} {open
        ? 'bg-brand text-surface! hover:bg-brand'
        : ''}"
      use:tooltip={"Share Task"}
      aria-expanded={open}
      onclick={toggle}><Message size={1.5} /></button
    >
  {/snippet}

  {#snippet content({ close })}
    <div class="border-b border-surface-alt p-2">
      <input
        bind:this={searchInput}
        bind:value={query}
        type="text"
        placeholder="Search friends..."
        class="search-field"
      />
    </div>
    {#if error}
      <div class="p-2 text-sm text-red-500">{error}</div>
    {/if}
    <div class="grid grid-cols-3 overflow-y-auto gap-2 p-2">
      {#each filteredFriends as friend (friend.id)}
        <button
          class="flex flex-col items-center justify-between gap-2 secondary-button bg-surface rounded-lg {selectedFriendIds.has(
            friend.id,
          )
            ? 'bg-surface-alt-hover !'
            : 'hover:bg-surface-hover'}"
          aria-label="go to profile"
          use:tooltip={"Share with " + friend.displayName}
          onclick={() => toggleFriend(friend.id)}
        >
          <Profileicon userId={friend.id} clickable={false} size={2} />
          <div class="text-lg font-semibold">
            {friend.displayName}
          </div>
        </button>
      {:else}
        <p class="col-span-3 p-2 text-sm text-text-muted">
          No friends available to share with.
        </p>
      {/each}
    </div>
    {#if selectedFriendIds.size}
      <button
        class="primary-button w-fit absolute bottom-2 right-2"
        disabled={sharing}
        onclick={() => share(close)}
        >{sharing
          ? "Sharing..."
          : "Share " +
            (selectedFriendIds.size > 1
              ? "with " + selectedFriendIds.size + " friends"
              : "with " +
                friends.find((f) => selectedFriendIds.has(f.id))
                  ?.displayName)}</button
      >
    {/if}
  {/snippet}
</Popover>
