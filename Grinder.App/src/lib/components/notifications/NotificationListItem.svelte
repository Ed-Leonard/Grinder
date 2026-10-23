<script lang="ts">
  import { goto } from "$app/navigation";
  import { resolve } from "$app/paths";
  import type { NotificationDto } from "$lib/api/notifications";
  import NotificationIcon from "./NotificationIcon.svelte";
  import { RelationshipStatus } from "$lib/types";
  import {
    iconFor,
    isFollowNotification,
    messageFor,
    timeAgo,
  } from "$lib/utils/notificationDisplay";
  import FollowButton from "../Friends/FollowButton.svelte";
  import Uprightarrow from "../svgs/uprightarrow.svelte";
  import Xicon from "../svgs/xicon.svelte";
  import { tooltip } from "$lib/utils/tooltip";

  let {
    notification,
    relationshipStatus = $bindable(),
    onMarkRead,
    onDismiss,
  }: {
    notification: NotificationDto;
    relationshipStatus: RelationshipStatus;
    onMarkRead: () => void;
    onDismiss: () => void;
  } = $props();
</script>

<div
  class="inline-flex w-full items-start gap-2 px-2 py-4 md:p-4 transition-colors {notification.read
    ? ''
    : 'bg-surface-alt'} "
>
  <NotificationIcon
    type={iconFor(notification)}
    read={notification.read}
    count={notification.messageCount}
  />

  <div class="inline-flex justify-between items-center w-full">
    <div class="flex flex-wrap items-center pt-0.5 gap-4">
      <div class="flex flex-col gap-1">
        <a
          class="hover:underline inline-flex items-center gap-2"
          href={resolve(`/profile/${notification.senderId}`)}
        >
          {messageFor(notification)}
          {#if notification.reaction}
            <img
              src={resolve(
                `/reactions/${notification.reaction.fileName}` as any,
              )}
              alt={notification.reaction.name}
              loading="lazy"
              class="pointer-events-none h-8 w-8 object-contain"
            />
          {/if}
        </a>

        <p class="text-xs text-text-muted">
          {timeAgo(notification.createdAt)}
        </p>
      </div>
      {#if isFollowNotification(notification)}
        <FollowButton
          userId={notification.senderId}
          bind:relationshipStatus
          size="sm"
        />
      {/if}
    </div>

    <div class="flex items-center gap-1">
      {#if !notification.read}
        <button
          class="text-brand icon-button"
          aria-label="Mark as read"
          use:tooltip={"Mark as read"}
          onclick={onMarkRead}
        >
          <svg
            class="w-4 h-4"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
          >
            <path
              stroke-linecap="round"
              stroke-linejoin="round"
              stroke-width="2"
              d="M4.5 12.75l6 6 9-13.5"
            />
          </svg>
        </button>
      {/if}
      {#if iconFor(notification) === "Message"}
        <button
          class="icon-button"
          aria-label="Open chat"
          use:tooltip={"Open chat"}
          onclick={() => goto(resolve(`/chat/${notification.senderId}` as any))}
        >
          <Uprightarrow />
        </button>
      {:else if !isFollowNotification(notification)}
        <button
          class="icon-button"
          aria-label="Go to task"
          use:tooltip={"Go to task"}
          onclick={() =>
            goto(resolve(`/tasks/${notification.entityId}` as any))}
        >
          <Uprightarrow />
        </button>
      {/if}
      <button
        class="icon-button"
        aria-label="Dismiss"
        use:tooltip={"Dismiss"}
        onclick={onDismiss}
      >
        <Xicon size={1} />
      </button>
    </div>
  </div>
</div>
