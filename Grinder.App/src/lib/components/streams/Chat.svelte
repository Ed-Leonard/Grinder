<script lang="ts">
  import { onMount } from "svelte";
  import { user } from "$lib/stores/auth";
  import { fetchLiveChat } from "$lib/api/livestreams";
  import {
    joinLiveChat,
    leaveLiveChat,
    onLiveChatMessage,
    sendLiveChatMessage,
    type LiveChatMessage,
  } from "$lib/hubs/chatHub";
  import autogrow from "$lib/utils/autogrow";
  import ErrorPopup from "$lib/components/utils/Error.svelte";

  let { roomName }: { roomName: string } = $props();

  const LIMIT = 20;
  const MAX_LENGTH = 256;

  let messages = $state<LiveChatMessage[]>([]);
  let page = $state(1);
  let hasMore = $state(true);
  let loading = $state(false);
  let error = $state("");
  let draft = $state("");
  let sending = $state(false);
  let sendFailed = $state(false);
  let scroller = $state<HTMLDivElement>();
  let sentinel = $state<HTMLDivElement>();

  let newestFirst = $derived([...messages].reverse());

  async function loadNextPage() {
    if (loading || !hasMore || !scroller) return;
    loading = true;
    try {
      const nextPage = await fetchLiveChat(roomName, page, LIMIT);
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
      await sendLiveChatMessage(roomName, content);
      sendFailed = false;
      if (draft.trim() === content) draft = "";
    } catch {
      sendFailed = true;
    } finally {
      sending = false;
    }
  }

  function handleSend() {
    const content = draft.trim();
    if (!content || content.length > MAX_LENGTH || sending) return;
    send(content);
  }

  function handleKeydown(event: KeyboardEvent) {
    if (event.key === "Enter" && !event.shiftKey && !event.isComposing) {
      event.preventDefault();
      handleSend();
    }
  }
  const palette = [
    "text-red-600 dark:text-red-300",
    "text-orange-600 dark:text-orange-300",
    "text-amber-600 dark:text-amber-300",
    "text-green-600 dark:text-green-300",
    "text-teal-600 dark:text-teal-300",
    "text-sky-600 dark:text-sky-300",
    "text-violet-600 dark:text-violet-300",
    "text-pink-600 dark:text-pink-300",
  ];

  function userColor(name: string): string {
    let h = 2166136261;
    for (let i = 0; i < name.length; i++) {
      h ^= name.charCodeAt(i);
      h = Math.imul(h, 16777619);
      h ^= h >>> 16; // mix high bits into the low bits
    }
    return palette[(h >>> 0) % palette.length];
  }

  onMount(() => {
    joinLiveChat(roomName).catch(() => {
      error = "Failed to join chat";
    });

    const off = onLiveChatMessage((msg) => {
      if (msg.roomName !== roomName || messages.some((m) => m.id === msg.id))
        return;
      const mine = msg.senderId === $user?.id;
      messages = [...messages, msg];
      if (scroller && (mine || scroller.scrollTop > -150)) {
        scroller.scrollTop = 0;
      }
    });

    return () => {
      off();
      leaveLiveChat(roomName).catch(() => {});
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

<div class="flex h-full min-h-0 flex-col">
  <div
    bind:this={scroller}
    class="flex min-h-0 w-full flex-1 flex-col-reverse gap-2 overflow-y-auto px-2"
  >
    {#if error}
      <ErrorPopup {error} />
    {/if}
    {#if messages.length === 0 && !loading}
      <p class="py-2 text-center text-sm text-text-muted">No messages yet</p>
    {/if}
    {#each newestFirst as message, i (message.id)}
      {@const isMine = message.senderId === $user?.id}
      <div
        class="p-2 pt-1 text-sm border-b first:border-none border-surface-alt"
      >
        <div class="flex flex-row gap-2 items-baseline">
          <span class="font-semibold {userColor(message.senderName)}"
            >{isMine ? "You" : message.senderName}</span
          >
          <span class="wrap-anywhere">{message.content}</span>
        </div>
      </div>
    {/each}
    <div bind:this={sentinel} class="h-1 w-full shrink-0"></div>
  </div>

  {#if sendFailed && draft.trim()}
    <div class="px-2 pt-2">
      <div
        role="alert"
        class="flex items-center justify-between gap-2 rounded-lg border border-red-500 bg-surface-alt px-4 py-2 text-sm text-red-500"
      >
        <span>Failed to send message, please try again</span>
        <button
          type="button"
          onclick={handleSend}
          disabled={sending}
          class="secondary-button"
        >
          Retry
        </button>
      </div>
    </div>
  {/if}

  <form
    class="flex shrink-0 items-center gap-2 border-t border-surface-alt p-4"
    style="padding-bottom: max(0.75rem, env(safe-area-inset-bottom));"
    onsubmit={(event) => {
      event.preventDefault();
      handleSend();
    }}
  >
    <div class="floating relative min-w-0 flex-1">
      <textarea
        bind:value={draft}
        use:autogrow={draft}
        onkeydown={handleKeydown}
        rows="1"
        maxlength={MAX_LENGTH}
        placeholder="Send a message..."
        id="chatMessage"
        class="text-area-field peer"
      ></textarea>
      <label class="input-label" for="chatMessage">
        Message (1-{MAX_LENGTH} Characters)
        {draft.length > 0 ? `(${draft.length} / ${MAX_LENGTH})` : ""}
      </label>
    </div>
    <button
      type="submit"
      disabled={!draft.trim() || sending}
      class="primary-button shrink-0 py-2"
    >
      Send
    </button>
  </form>
</div>
