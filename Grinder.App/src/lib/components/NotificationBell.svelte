<script lang="ts">
  import { resolve } from "$app/paths";
  import { onMount, onDestroy } from "svelte";
  import {
    markAsRead,
    markAllAsRead,
    type NotificationDto,
  } from "$lib/api/notifications";
  import { notificationStore } from "$lib/stores/notifications";
  import { messageFor, timeAgo } from "$lib/utils/notificationDisplay";
  import { portal } from "$lib/utils/portal";
  import { tooltip } from "$lib/utils/tooltip";
  import { browser } from "$app/environment";
  import { scale, slide } from "svelte/transition";
  import { goto } from "$app/navigation";

  let isOpen = $state(false);

  let containerEl: HTMLDivElement;
  let bellEl: HTMLButtonElement;
  let dropdownEl: HTMLDivElement | undefined = $state();
  let dropdownPos = $state({ top: 0, right: 0 });

  // reactive, driven entirely by the store — no fetch, no polling
  let unreadCount = $derived($notificationStore.unreadCount);
  let notifications = $derived($notificationStore.items);

  function toggleDropdown() {
    isOpen = !isOpen;
    if (isOpen) {
      const rect = bellEl.getBoundingClientRect();
      dropdownPos = {
        top: rect.bottom + 8,
        right: window.innerWidth - rect.right,
      };
    }
  }

  function closeDropdown() {
    isOpen = false;
  }

  function handleClickOutside(event: MouseEvent) {
    if (!isOpen) return;
    const target = event.target as Node;
    const clickedContainer = containerEl?.contains(target);
    const clickedDropdown = dropdownEl?.contains(target);
    if (!clickedContainer && !clickedDropdown) {
      closeDropdown();
    }
  }

  async function handleMarkRead(notification: NotificationDto) {
    if (notification.read) return;
    try {
      await markAsRead(notification.id);
      notificationStore.markReadLocal(notification.id);
    } catch (err) {
      console.error(err);
    }
  }

  async function handleMarkAllRead() {
    try {
      await markAllAsRead();
      notificationStore.markAllReadLocal();
    } catch (err) {
      console.error(err);
    }
  }

  onMount(() => {
    document.addEventListener("click", handleClickOutside);
  });

  onDestroy(() => {
    if (browser) {
      document.removeEventListener("click", handleClickOutside);
    }
  });
</script>

<div class="relative" bind:this={containerEl}>
  <button
    bind:this={bellEl}
    onclick={toggleDropdown}
    class="relative icon-button {isOpen ? 'bg-brand text-surface ' : ''}"
    aria-label="Notifications"
    use:tooltip={"Notifications"}
  >
    <svg class="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
      <path
        stroke-linecap="round"
        stroke-linejoin="round"
        stroke-width="2"
        d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9"
      />
    </svg>

    {#if unreadCount > 0}
      {#key unreadCount}
        <span
          in:scale={{ duration: 400, start: 0.6 }}
          class="absolute -top-1 -right-1 flex items-center justify-center min-w-5 min-h-5 text-xs font-semibold text-white bg-red-500 rounded-full px-1"
        >
          {unreadCount > 99 ? "99+" : unreadCount}
        </span>
      {/key}
    {/if}
  </button>

  {#if isOpen}
    <div
      use:portal
      bind:this={dropdownEl}
      class="w-60 xs:w-80 rounded-lg shadow-lg border border-surface-alt bg-surface z-50"
      style={`position: fixed; top: ${dropdownPos.top}px; right: ${dropdownPos.right}px;`}
    >
      <div
        class="flex items-center justify-between px-4 py-3 border-b border-surface-alt"
      >
        <h3 class="font-semibold">Notifications</h3>
        {#if unreadCount > 0}
          <button
            class="text-sm text-blue-500 hover:text-blue-700 transition"
            onclick={handleMarkAllRead}
          >
            Mark all read
          </button>
        {/if}
      </div>

      <div class="max-h-64 md:max-h-96 overflow-y-auto">
        {#if notifications.length === 0}
          <div class="px-4 py-8 text-center text-text-muted text-sm">
            No notifications yet
          </div>
        {:else}
          {#each notifications as notification (notification.id)}
            <button
              in:slide
              class="relative w-full text-left px-4 py-3 flex gap-3 border-b border-surface-alt last:border-0 transition bg-surface hover:bg-surface-hover"
              onclick={() => {
                handleMarkRead(notification);
                isOpen = false;
                if (notification.type === "Follow") {
                  goto(resolve(`/profile/${notification.senderId}` as any));
                } else if (notification.type === "Message") {
                  goto(resolve(`/chat/${notification.senderId}` as any));
                } else {
                  goto(resolve(`/tasks/${notification.entityId}` as any));
                }
              }}
            >
              {#if !notification.read && notification.type === "Message" && notification.messageCount > 1}
                <span
                  class="absolute top-1 right-1 min-w-4 h-4 px-1 rounded-full bg-red-500 text-white text-[10px] leading-4 text-center font-semibold"
                >
                  {notification.messageCount}
                </span>
              {/if}
              <div
                class="shrink-0 w-2 h-2 mt-2 rounded-full {notification.read
                  ? 'bg-transparent'
                  : 'bg-blue-500'}"
              ></div>
              <div class="flex-1 min-w-0">
                <p class="text-sm inline-flex items-center gap-2">
                  {messageFor(notification)}
                  {#if notification.reaction}
                    <img
                      src={resolve(
                        `/reactions/${notification.reaction.fileName}` as any,
                      )}
                      alt={notification.reaction.name}
                      loading="lazy"
                      class="pointer-events-none h-5 w-5 object-contain"
                    />
                  {/if}
                </p>
                <p class="text-xs text-text-muted mt-0.5">
                  {timeAgo(notification.createdAt)}
                </p>
              </div>
            </button>
          {/each}
        {/if}
      </div>

      <div class="px-4 py-2 border-t border-surface-alt text-center">
        <a
          href={resolve("/notifications" as any)}
          class="text-sm text-blue-500 hover:text-blue-700 hover:underline"
          onclick={closeDropdown}
        >
          View all
        </a>
      </div>
    </div>
  {/if}
</div>
