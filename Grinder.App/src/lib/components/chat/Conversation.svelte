<script lang="ts">
  import { onMount, tick } from "svelte";
  import { fetchConversation, type Message } from "$lib/api/directMessages";
  import {
    sendChatMessage,
    onChatMessage,
    onConversationRead,
    markConversationRead,
  } from "$lib/hubs/chatHub";
  import { user } from "$lib/stores/auth";
  import Profileicon from "../svgs/profileicon.svelte";
  import { getUserProfile } from "$lib/api/profile";
  import { type User } from "$lib/types";
  import Leftarrow from "$lib/components/svgs/leftarrow.svelte";
  import { resolve } from "$app/paths";
  import { tooltip } from "$lib/utils/tooltip";
  import ErrorPopup from "$lib/components/utils/Error.svelte";
  import { fetchReactions, type Reaction } from "$lib/api/reaction";
  import MessageContent from "./MessageContent.svelte";
  import ReactionPickerButton from "$lib/components/reactions/ReactionPickerButton.svelte";
  import autoGrow from "$lib/utils/autogrow";
  import SharedTaskCard from "$lib/components/chat/SharedTaskCard.svelte";

  let {
    otherUserId,
  }: {
    otherUserId: number;
  } = $props();

  const LIMIT = 20;
  const MAX_LENGTH = 256;

  let textarea = $state<HTMLTextAreaElement>();
  let reactions = $state<Reaction[]>([]);
  let reactionsByName = $derived(new Map(reactions.map((r) => [r.name, r])));
  let messages = $state<Message[]>([]);
  let page = $state(1);
  let hasMore = $state(true);
  let loading = $state(false);
  let error = $state<string>();
  let otherUser = $state<User>();
  let firstUnreadId = $derived(
    messages.find((m) => !m.read && m.senderId === otherUserId)?.id,
  );
  let lastMineMessageId = $derived(
    [...messages].reverse().find((m) => m.senderId === $user?.id)?.id,
  );
  let pendingRead = false;

  let scroller = $state<HTMLDivElement>();
  let sentinel = $state<HTMLDivElement>();
  let draft = $state("");
  let sending = $state(false);
  let sendFailed = $state(false);

  let reactionError = $state<string>();

  function markReadIfVisible() {
    if (document.visibilityState !== "visible") {
      pendingRead = true;
      return;
    }
    pendingRead = false;
    markConversationRead(otherUserId).catch(() => {});
  }

  function showReactionError() {
    reactionError = `Reaction cannot be added, messages are limited to ${MAX_LENGTH} characters`;
  }

  function isSameDay(a: string, b: string) {
    return new Date(a).toDateString() === new Date(b).toDateString();
  }

  let messagesWithDividers = $derived(
    messages
      .map((m, i) => ({
        ...m,
        showDateDivider:
          i === 0 || !isSameDay(m.sentAt, messages[i - 1].sentAt),
      }))
      .reverse(),
  );

  async function loadNextPage() {
    const el = scroller;
    if (loading || !hasMore || !el) return;
    loading = true;
    try {
      const nextPage = await fetchConversation(otherUserId, page, LIMIT);

      hasMore = nextPage.hasMore;
      messages = [...nextPage.messages, ...messages];
      page += 1;
    } catch {
      error = "Failed to load messages";
    } finally {
      loading = false;
    }
  }

  async function send(content: string) {
    if (sending) return;
    sending = true;
    try {
      await sendChatMessage(otherUserId, content);
      sendFailed = false;
      // only clear if they haven't edited the draft while it was sending
      if (draft.trim() === content) draft = "";
    } catch {
      sendFailed = true;
    } finally {
      sending = false;
    }
  }

  function handleSend() {
    reactionError = undefined;
    const content = draft.trim();
    if (!content || sending) return;
    send(content);
  }

  function handleKeydown(e: KeyboardEvent) {
    if (e.key === "Enter" && !e.shiftKey && !e.isComposing) {
      e.preventDefault();
      handleSend();
    }
  }

  function insertReaction(reaction: Reaction) {
    const token = `:${reaction.name}:`;
    const start = textarea?.selectionStart ?? draft.length;
    const end = textarea?.selectionEnd ?? draft.length;
    if (draft.length - (end - start) + token.length > MAX_LENGTH) {
      showReactionError();
      return;
    }

    reactionError = undefined;
    draft = draft.slice(0, start) + token + draft.slice(end);

    tick().then(() => {
      const pos = start + token.length;
      textarea?.focus();
      textarea?.setSelectionRange(pos, pos);
    });
  }

  onMount(() => {
    getUserProfile(otherUserId, fetch)
      .then((r) => r.json())
      .then((u) => (otherUser = u))
      .catch(() => {});

    fetchReactions()
      .then((r) => (reactions = r))
      .catch(() => {});

    const offMessage = onChatMessage((msg) => {
      const mine = msg.senderId === $user?.id;
      const inThisConversation = mine
        ? msg.receiverId === otherUserId
        : msg.senderId === otherUserId;
      if (!inThisConversation || messages.some((m) => m.id === msg.id)) return;

      // Incoming messages are marked read locally so the "Unread messages" divider
      // doesn't appear for live messages; own messages keep their real (unread) status.
      messages = [...messages, mine ? msg : { ...msg, read: true }];
      if (!mine) markReadIfVisible();

      // Always follow your own messages, but only follow incoming ones if the user is near the bottom.
      if (scroller && (mine || scroller.scrollTop > -150)) {
        scroller.scrollTop = 0;
      }
    });

    const offRead = onConversationRead((readerId, lastReadMessageId) => {
      if (readerId !== otherUserId) return;
      messages = messages.map((m) =>
        m.senderId === $user?.id && m.id <= lastReadMessageId
          ? { ...m, read: true }
          : m,
      );
    });

    return () => {
      offMessage();
      offRead();
    };
  });
  $effect(() => {
    if (!sentinel || !scroller) return;
    void page;

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0].isIntersecting) loadNextPage();
      },
      { root: scroller, rootMargin: "200px 0px 0px 0px" },
    );
    observer.observe(sentinel);
    return () => observer.disconnect();
  });
</script>

<svelte:document
  onvisibilitychange={() => {
    if (pendingRead) markReadIfVisible();
  }}
/>

<div class="w-full h-full flex flex-col items-center px-2">
  <div class="inline-flex px-2 pb-2 items-center justify-start w-full gap-4">
    <a
      href={resolve("/chat")}
      class="icon-button"
      use:tooltip={"Back to messages"}
    >
      <Leftarrow size={1.5} />
    </a>
    <Profileicon userId={otherUserId} size={3} />
    <h2>{otherUser?.displayName}</h2>
  </div>
  <hr class="w-full text-surface-alt-hover" />
  <div
    bind:this={scroller}
    class="flex flex-col-reverse w-full flex-1 min-h-0 gap-2 md:p-4 overflow-y-auto"
  >
    {#if error}
      <ErrorPopup {error} />
    {/if}
    {#each messagesWithDividers as message, i (message.id)}
      {@const previousMessage = messagesWithDividers[i + 1] ?? null}
      {@const nextMessage = messagesWithDividers[i - 1] ?? null}
      {@const isMine = message.senderId === $user?.id}
      {@const showIcon = nextMessage?.senderId !== message.senderId}

      {#if isMine && message.id === lastMineMessageId && message.read}
        <p class="self-end text-xs text-text-muted pr-1">Read</p>
      {/if}
      <div
        class="max-w-64 sm:max-w-lg {isMine
          ? 'self-end flex-row-reverse'
          : 'self-start flex-row'}
    {previousMessage?.senderId !== message.senderId ? 'mt-4' : ''}
    flex items-end gap-2 relative"
      >
        {#if !isMine}
          <div class="w-8 shrink-0">
            {#if showIcon}
              <Profileicon userId={message.senderId} />
            {/if}
          </div>
        {/if}

        {#if message.content}
          <div
            class="{isMine
              ? 'bg-brand text-surface rounded-br-sm'
              : 'bg-surface-alt rounded-bl-sm'} {previousMessage?.senderId ===
              message.senderId &&
            !message.showDateDivider &&
            message.id !== firstUnreadId
              ? isMine
                ? 'rounded-tr-sm'
                : 'rounded-tl-sm'
              : ''} p-2 px-4 text-sm sm:text-base
    rounded-3xl wrap-anywhere whitespace-pre-wrap"
          >
            <MessageContent content={message.content} {reactionsByName} />
          </div>
        {:else if message.sharedTaskId}
          <div class={isMine ? "rounded-br-sm" : "rounded-bl-sm"}>
            <SharedTaskCard task={message.sharedTask} />
          </div>
        {/if}
      </div>

      {#if message.showDateDivider}
        <div class="date-divider self-center text-sm text-text-muted my-2">
          {new Date(message.sentAt).toLocaleDateString(undefined, {
            weekday: "long",
            month: "short",
            day: "numeric",
          })}
        </div>
      {/if}
      {#if message.id === firstUnreadId}
        <div class="w-full flex flex-col gap-2 my-4 items-center">
          <hr class="w-full text-surface-alt-hover" />
          <p class="text-text-muted text-sm">Unread messages</p>
        </div>
      {/if}
    {/each}
    <div bind:this={sentinel} class="h-1 w-full shrink-0"></div>
  </div>
  {#if sendFailed && draft.trim()}
    <div class="w-full pt-2 md:px-4">
      <div
        role="alert"
        class="flex items-center justify-between gap-2 rounded-lg border border-red-500 bg-surface-alt px-4 py-2 text-sm text-red-500 alert-flash"
      >
        <span>Failed to send message, please try again</span>
        <button
          type="button"
          onclick={handleSend}
          disabled={sending}
          class="secondary-button bg-transparent hover:bg-red-500/20"
        >
          Retry
        </button>
      </div>
    </div>
  {/if}
  {#if reactionError}
    <div class="w-full pt-2 px-4 [&>div]:mb-0">
      <ErrorPopup error={reactionError} />
    </div>
  {/if}
  <form
    class="w-full flex flex-col xs:flex-row items-end xs:items-center gap-2 pt-2 md:px-4"
    onsubmit={(e) => {
      e.preventDefault();
      handleSend();
    }}
  >
    <div class="w-full floating relative">
      <textarea
        bind:value={draft}
        use:autoGrow={draft}
        oninput={() => (reactionError = undefined)}
        onkeydown={handleKeydown}
        rows="1"
        id="sendMessage"
        maxlength={MAX_LENGTH}
        placeholder="Write a message..."
        class="text-area-field peer"
      ></textarea>
      <label class="input-label" for="sendMessage">
        Message (0-{MAX_LENGTH} Characters) {draft.length > 0
          ? "(" + draft.length + " /" + MAX_LENGTH + ")"
          : ""}
      </label>
    </div>
    <div class="flex items-center gap-2">
      <ReactionPickerButton onPick={insertReaction} />
      <button
        type="submit"
        disabled={!draft.trim() || sending}
        class="primary-button"
      >
        Send
      </button>
    </div>
  </form>
</div>
