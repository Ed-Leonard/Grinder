<script lang="ts">
  import { page } from "$app/state";
  import { onMount, tick } from "svelte";
  import { user } from "$lib/stores/auth";
  import Profileicon from "$lib/components/svgs/profileicon.svelte";
  import ErrorPopup from "$lib/components/utils/Error.svelte";
  import { shortTimeAgo } from "$lib/utils/dateUtils.svelte";
  import {
    fetchConversationPreviews,
    type ConversationPreview,
  } from "$lib/api/directMessages";
  import { onChatMessage, type ChatMessage } from "$lib/hubs/chatHub";
  import { resolve } from "$app/paths";
  import { goto } from "$app/navigation";
  import MessageContent from "$lib/components/chat/MessageContent.svelte";
  import { fetchReactions, type Reaction } from "$lib/api/reaction";
  import Pluscircle from "$lib/components/svgs/pluscircle.svelte";
  import Popover from "$lib/components/Popover.svelte";
  import { getFriends, type Friend } from "$lib/api/follows";

  let reactions = $state<Reaction[]>([]);
  let reactionsByName = $derived(new Map(reactions.map((r) => [r.name, r])));

  let { children } = $props();

  let conversations = $state<ConversationPreview[]>([]);
  let loading = $state(true);
  let error = $state("");

  onMount(() => {
    fetchReactions().then((r) => (reactions = r)).catch(() => {});
    fetchConversationPreviews()
      .then((r) => (conversations = mergeConversations(r, conversations)))
      .catch((err) => {
        console.error(err);
        error = "Failed to load chat";
      })
      .finally(() => (loading = false));

    return onChatMessage(handleLiveMessage);
  });

  // Combines a freshly-fetched list with whatever's currently in state, keeping
  // whichever version of each conversation is newer so a live update that arrived
  // while the fetch was in flight isn't clobbered by the (now stale) response.
  function mergeConversations(
    fetched: ConversationPreview[],
    current: ConversationPreview[],
  ): ConversationPreview[] {
    const currentById = new Map(current.map((c) => [c.otherUserId, c]));
    return fetched
      .map((f) => {
        const existing = currentById.get(f.otherUserId);
        return existing &&
          new Date(existing.lastMessageSentAt) > new Date(f.lastMessageSentAt)
          ? existing
          : f;
      })
      .sort(
        (a, b) =>
          new Date(b.lastMessageSentAt).getTime() -
          new Date(a.lastMessageSentAt).getTime(),
      );
  }

  function handleLiveMessage(msg: ChatMessage) {
    const mine = msg.senderId === $user?.id;
    const otherUserId = mine ? msg.receiverId : msg.senderId;

    const existing = conversations.find((c) => c.otherUserId === otherUserId);
    if (!existing) {
      // New conversation partner - just refetch rather than fetching their display name separately.
      fetchConversationPreviews()
        .then((r) => (conversations = mergeConversations(r, conversations)))
        .catch(() => {});
      return;
    }

    const viewingThisConversation = selectedUserId === otherUserId;
    const unreadCount =
      !mine && !viewingThisConversation ? existing.unreadCount + 1 : 0;
    const updated: ConversationPreview = {
      ...existing,
      lastMessagePreview: msg.content ?? "Shared a task",
      lastMessageSenderId: msg.senderId,
      lastMessageSentAt: msg.sentAt,
      unreadCount,
      hasUnread: unreadCount > 0,
    };

    conversations = [
      updated,
      ...conversations.filter((c) => c.otherUserId !== otherUserId),
    ];
  }

  function previewPrefix(conversation: ConversationPreview): string {
    return conversation.lastMessageSenderId === $user?.id ? "You: " : "";
  }

  let friends = $state<Friend[]>([]);
  let friendsError = $state("");
  let friendQuery = $state("");
  let friendSearchInput = $state<HTMLInputElement | null>(null);

  let filteredFriends = $derived(
    friendQuery.trim() === ""
      ? friends
      : friends.filter((f) =>
          f.displayName
            .toLowerCase()
            .includes(friendQuery.trim().toLowerCase()),
        ),
  );

  function handleNewMessageClick() {
    friendQuery = "";
    friendsError = "";

    const currentUserId = $user?.id;
    if (currentUserId == null) {
      friendsError = "Failed to load friends";
      return;
    }

    getFriends(currentUserId)
      .then((result) => (friends = result))
      .catch((err) => {
        console.error("Failed to load friends:", err);
        friendsError = "Failed to load friends";
      });
  }

  function startConversation(friendId: number, close: () => void) {
    close();
    goto(resolve(`/chat/${friendId}`));
  }

  let selectedUserId = $derived(
    page.params.slug ? Number(page.params.slug) : null,
  );

  $effect(() => {
    if (selectedUserId === null) return;

    const convo = conversations.find((c) => c.otherUserId === selectedUserId);
    if (!convo || !convo.hasUnread) return;

    conversations = conversations.map((c) =>
      c.otherUserId === selectedUserId
        ? { ...c, hasUnread: false, unreadCount: 0 }
        : c,
    );
  });
</script>

<div class="h-full min-h-0 bg-surface-alt md:p-4">
  <div
    class="card h-full min-h-0 flex overflow-hidden py-4 p-0 md:border-surface-alt-hover"
  >
    <aside
      class="w-full md:w-1/4 shrink-0 flex-col border-surface-alt-hover md:border-r min-h-0 {selectedUserId
        ? 'hidden md:flex'
        : 'flex'}"
    >
      <div class="shrink-0 flex items-center p-2 px-4">
        <h1 class="text-left truncate">Your Messages</h1>
      </div>

      {#if error}
        <div class="px-4 pt-4">
          <ErrorPopup {error} />
        </div>
      {/if}

      <div class="flex-1 min-h-0 overflow-y-auto">
        {#if loading}
          {#each { length: 6 } as _}
            <div class="flex items-center gap-4 px-4 py-4 animate-pulse">
              <div
                class="w-12 h-12 rounded-full bg-surface-alt-hover shrink-0"
              ></div>
              <div class="flex-1 space-y-2">
                <div class="h-3.5 bg-surface-hover rounded w-1/3"></div>
                <div class="h-3 bg-surface-hover rounded w-2/3"></div>
              </div>
            </div>
          {/each}
        {:else if conversations.length === 0}
          <div class="flex flex-col items-center gap-2 px-4 py-14 text-center">
            <p class="font-medium text-text-muted">No messages yet</p>
            <p class="text-sm text-text-muted">
              Conversations you start will show up here.
            </p>
          </div>
        {:else}
          {#each conversations as conversation (conversation.otherUserId)}
            <a
              type="button"
              href={resolve(`/chat/${conversation.otherUserId}`)}
              class="w-full flex items-center gap-4 p-4 text-left transition-colors {selectedUserId ===
              conversation.otherUserId
                ? 'bg-surface-alt-hover'
                : 'hover:bg-surface-hover'}"
            >
              <div class="relative shrink-0">
                <Profileicon
                  userId={conversation.otherUserId}
                  size={3}
                  clickable={false}
                />
                {#if conversation.hasUnread}
                  <span
                    class="absolute bottom-0 right-0 w-3 h-3 rounded-full bg-red-500 border-2 border-surface"
                  ></span>
                {/if}
              </div>
              <div class="flex-1 min-w-0">
                <p class="truncate text-lg font-semibold">
                  {conversation.otherUserDisplayName}
                </p>
                <p
                  class="truncate text-sm {conversation.hasUnread
                    ? 'font-medium'
                    : 'text-text-muted'}"
                >
                  {previewPrefix(conversation)}<MessageContent
                    content={conversation.lastMessagePreview}
                    {reactionsByName}
                    imgClass="h-5 w-5"
                  /> · {shortTimeAgo(conversation.lastMessageSentAt)}
                </p>
              </div>
              {#if conversation.hasUnread}
                <span
                  class="inline-flex items-center justify-center min-w-5 min-h-5 px-1 text-xs font-semibold text-white bg-red-500 rounded-full shrink-0"
                >
                  {conversation.unreadCount > 99
                    ? "99+"
                    : conversation.unreadCount}
                </span>
              {/if}
            </a>
          {/each}
        {/if}
      </div>
      <div class="shrink-0 border-surface-alt-hover p-2">
        <Popover
          onopen={handleNewMessageClick}
          vertical="up"
          horizontal="right"
          class="w-full block"
          panelClass="flex max-h-68 overflow-y-auto w-full flex-col"
        >
          {#snippet trigger({ open, toggle })}
            <button
              class="primary-button inline-flex gap-2 items-center w-full justify-center"
              aria-expanded={open}
              onclick={toggle}
            >
              <Pluscircle /> New Message
            </button>
          {/snippet}

          {#snippet content({ close })}
            <div class="border-surface-alt p-2">
              <input
                bind:this={friendSearchInput}
                bind:value={friendQuery}
                type="text"
                placeholder="Search friends..."
                class="search-field"
              />
            </div>
            {#if friendsError}
              <div class="p-2 text-sm text-red-500">{friendsError}</div>
            {/if}
            <div class="flex-1 overflow-y-auto p-2 flex flex-col gap-1">
              {#each filteredFriends as friend (friend.id)}
                <button
                  class="flex items-center gap-3 p-2 rounded-lg text-left hover:bg-surface-hover transition-colors"
                  onclick={() => startConversation(friend.id, close)}
                >
                  <Profileicon
                    userId={friend.id}
                    clickable={false}
                    size={2}
                  />
                  <div class="font-semibold truncate">
                    {friend.displayName}
                  </div>
                </button>
              {:else}
                <p class="p-2 text-sm text-text-muted">
                  No friends to message.
                </p>
              {/each}
            </div>
          {/snippet}
        </Popover>
      </div>
    </aside>

    <section
      class="flex-1 min-w-0 min-h-0 flex-col items-center justify-center {selectedUserId
        ? 'flex'
        : 'hidden md:flex'}"
    >
      {@render children()}
    </section>
  </div>
</div>
