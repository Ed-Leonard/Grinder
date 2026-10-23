<script lang="ts">
  import ErrorPopup from "$lib/components/utils/Error.svelte";
  import BasePage from "$lib/components/utils/BasePage.svelte";
  import NotificationListItem from "$lib/components/notifications/NotificationListItem.svelte";
  import { onMount } from "svelte";
  import {
    fetchNotifications,
    markAsRead,
    markAllAsRead,
    dismissNotification,
    type NotificationDto,
    type NotificationType,
  } from "$lib/api/notifications";
  import { getRelationshipStatuses } from "$lib/api/follows";
  import { isFollowNotification } from "$lib/utils/notificationDisplay";
  import { RelationshipStatus } from "$lib/types";
  import { notificationStore } from "$lib/stores/notifications";
  import autoheight from "$lib/utils/autoheight";
  import { scale, slide } from "svelte/transition";

  let notifications = $state<NotificationDto[]>([]);
  let page = $state(1);
  let totalPages = $state(1);
  let filterType = $state<NotificationType | null>(null);
  let loading = $state(false);
  let initialLoading = $state(true);
  let error = $state("");
  let relationshipByUser = $state<Record<number, RelationshipStatus>>({});
  let relationshipLoadingByUser = $state<Record<number, boolean>>({});

  let unreadCount = $derived($notificationStore.unreadCount);

  const typeOptions: { value: NotificationType | null; label: string }[] = [
    { value: null, label: "All" },
    { value: "Follow", label: "Friends" },
    { value: "Comment", label: "Comments" },
    { value: "React", label: "Reactions" },
    { value: "Message", label: "Messages" },
  ];

  async function refreshRelationshipStatuses(
    currentNotifications: NotificationDto[],
  ) {
    const senderIds = [
      ...new Set(
        currentNotifications
          .filter((n) => isFollowNotification(n))
          .map((n) => n.senderId),
      ),
    ];

    if (senderIds.length === 0) {
      return;
    }

    const nextRelationship = { ...relationshipByUser };
    const nextLoading = { ...relationshipLoadingByUser };

    for (const senderId of senderIds) {
      nextLoading[senderId] = true;
    }
    relationshipLoadingByUser = nextLoading;

    try {
      const statuses = await getRelationshipStatuses(senderIds);

      for (const [senderId, status] of Object.entries(statuses)) {
        nextRelationship[Number(senderId)] = status;
      }

      relationshipByUser = nextRelationship;
    } catch (err) {
      console.error(err);
      error = "Failed to load relationship statuses";
    } finally {
      for (const senderId of senderIds) {
        nextLoading[senderId] = false;
      }
      relationshipLoadingByUser = nextLoading;
    }
  }

  async function loadNotifications() {
    error = "";
    const loadingTimeout = setTimeout(() => {
      loading = true;
    }, 150);

    try {
      const result = await fetchNotifications({
        page,
        limit: 20,
        type: filterType,
      });
      notifications = result.items;
      totalPages = result.totalPages;
      await refreshRelationshipStatuses(notifications);
    } catch (err) {
      console.error(err);
      error = "Failed to load notifications";
    } finally {
      clearTimeout(loadingTimeout);
      loading = false;
      initialLoading = false;
    }
  }

  async function markRead(notification: NotificationDto) {
    if (notification.read) return;
    try {
      await markAsRead(notification.id);
      notification.read = true;
      notifications = notifications;
      notificationStore.markReadLocal(notification.id);
    } catch (err) {
      console.error(err);
      error = "Failed to mark notification as read";
    }
  }

  async function markAllRead() {
    try {
      await markAllAsRead();
      notifications = notifications.map((n) => ({ ...n, read: true }));
      notificationStore.markAllReadLocal();
    } catch (err) {
      console.error(err);
      error = "Failed to mark all notifications as read";
    }
  }

  async function dismiss(id: number) {
    try {
      await dismissNotification(id);
      notifications = notifications.filter((n) => n.id !== id);
    } catch (err) {
      console.error(err);
      error = "Failed to dismiss notification";
    }
  }

  function changeFilter(type: NotificationType | null) {
    filterType = type;
    page = 1;
    loadNotifications();
  }

  function prevPage() {
    if (page <= 1) return;
    page -= 1;
    loadNotifications();
  }

  function nextPage() {
    if (page >= totalPages) return;
    page += 1;
    loadNotifications();
  }

  let lastSeenSignature = $state<string | null>(null);

  $effect(() => {
    const latest = $notificationStore.items[0];
    if (!latest) return;

    const signature = `${latest.id}:${latest.read}:${latest.createdAt}`;

    if (lastSeenSignature === null) {
      lastSeenSignature = signature;
      return;
    }

    if (signature !== lastSeenSignature) {
      lastSeenSignature = signature;
      if (page === 1 && (filterType === null || filterType === latest.type)) {
        loadNotifications();
      }
    }
  });

  onMount(() => {
    loadNotifications();
  });
</script>

<BasePage
  maxWidth="max-w-3xl"
  class="bg-surface! md:bg-surface-alt! items-start md:items-center md:p-4"
>
  {#if error}
    <ErrorPopup {error} />
  {/if}

  <div class="card bg-surface flex flex-col gap-4 p-0 md:p-8">
    <div
      class="flex flex-wrap items-center justify-between w-full px-4 md:px-0"
    >
      <div class="flex items-center gap-2">
        <h1>Notifications</h1>
        {#if unreadCount > 0}
          {#key unreadCount}
            <span
              in:scale={{ duration: 400, start: 0.6 }}
              class="flex items-center justify-center min-w-6 min-h-6 px-1 pr-1.5 text-md font-semibold text-white bg-red-500 rounded-full"
            >
              {unreadCount > 99 ? "99+" : unreadCount}
            </span>
          {/key}
        {/if}
      </div>
      <button
        class="text-sm font-semibold text-brand hover:text-brand-hover transition-colors disabled:opacity-40 disabled:cursor-not-allowed"
        disabled={unreadCount === 0}
        onclick={markAllRead}
      >
        Mark all read
      </button>
    </div>

    <div class="flex flex-wrap justify-center items-center gap-2">
      {#each typeOptions as opt}
        <button
          class="secondary-button {opt.value === filterType
            ? 'bg-brand text-surface '
            : ''}"
          onclick={() => changeFilter(opt.value)}
        >
          {opt.label}
        </button>
      {/each}
    </div>

    <div
      class="md:rounded-lg transition-[height] duration-400 ease-out overflow-hidden"
    >
      <div use:autoheight>
        {#if initialLoading}
          {#each { length: 3 } as _}
            <div class="flex items-center gap-3 px-4 py-4 animate-pulse">
              <div
                class="w-9 h-9 rounded-full bg-surface-alt-hover shrink-0"
              ></div>
              <div class="flex-1 space-y-2">
                <div class="h-3.5 bg-surface-hover rounded w-3/4"></div>
                <div class="h-3 bg-surface-hover rounded w-1/4"></div>
              </div>
            </div>
          {/each}
        {:else if notifications.length === 0}
          <div class="flex flex-col items-center gap-2 py-14 text-center">
            <svg
              class="w-10 h-10 text-text-muted/50"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              <path
                stroke-linecap="round"
                stroke-linejoin="round"
                stroke-width="1.5"
                d="M15 17h5l-1.405-1.405A2.032 2.032 0 0118 14.158V11a6.002 6.002 0 00-4-5.659V5a2 2 0 10-4 0v.341C7.67 6.165 6 8.388 6 11v3.159c0 .538-.214 1.055-.595 1.436L4 17h5m6 0v1a3 3 0 11-6 0v-1m6 0H9"
              />
            </svg>
            <p class="text-text-muted font-medium">No notifications found</p>
            <p class="text-sm text-text-muted">
              You're all caught up{filterType ? " for this filter" : ""}.
            </p>
          </div>
        {:else}
          {#each notifications as notification (notification.id)}
            <div transition:slide>
              <NotificationListItem
                {notification}
                relationshipStatus={relationshipByUser[notification.senderId] ??
                  null}
                onMarkRead={() => markRead(notification)}
                onDismiss={() => dismiss(notification.id)}
              />
            </div>
          {/each}
        {/if}
      </div>
    </div>

    {#if totalPages > 1}
      <div class="flex justify-center items-center gap-4 mt-5">
        <button
          class="secondary-button px-4! py-2! text-sm"
          disabled={page <= 1}
          onclick={prevPage}
        >
          Previous
        </button>
        <span class="text-sm text-text-muted">Page {page} of {totalPages}</span>
        <button
          class="secondary-button px-4! py-2! text-sm"
          disabled={page >= totalPages}
          onclick={nextPage}
        >
          Next
        </button>
      </div>
    {/if}
  </div></BasePage
>
