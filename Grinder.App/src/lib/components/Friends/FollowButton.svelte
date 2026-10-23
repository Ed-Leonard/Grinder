<script lang="ts">
  import { followUser, unfollowUser } from "$lib/api/follows";
  import { RelationshipStatus } from "$lib/types";
  import { invalidateAll } from "$app/navigation";
  import HeartIcon from "$lib/components/svgs/heart.svelte";
  import Minus from "$lib/components/svgs/minuscircle.svelte";
  import Plus from "$lib/components/svgs/pluscircle.svelte";

  let {
    userId,
    relationshipStatus = $bindable(),
    size = "md",
    class: className = "",
  }: {
    userId: number;
    relationshipStatus: RelationshipStatus;
    size?: "sm" | "md";
    class?: string;
  } = $props();

  const statusLabel: Record<RelationshipStatus, string> = {
    [RelationshipStatus.Follow]: "Follow",
    [RelationshipStatus.Following]: "Following",
    [RelationshipStatus.Friends]: "Friends",
    [RelationshipStatus.FollowBack]: "Follow Back",
  };

  const sizeClasses = {
    sm: "text-sm px-3 py-1",
    md: "text-md px-4 py-1.5",
  };

  const iconRemSize = { sm: 1, md: 1.25 };

  let loading = $state(false);

  async function handleFollowClick(event: MouseEvent) {
    event.preventDefault();
    event.stopPropagation();

    if (loading) return;
    loading = true;

    try {
      if (
        relationshipStatus === RelationshipStatus.Follow ||
        relationshipStatus === RelationshipStatus.FollowBack
      ) {
        await followUser(userId);
        relationshipStatus =
          relationshipStatus === RelationshipStatus.FollowBack
            ? RelationshipStatus.Friends
            : RelationshipStatus.Following;
      } else if (relationshipStatus === RelationshipStatus.Following) {
        await unfollowUser(userId);
        relationshipStatus = RelationshipStatus.Follow;
      } else if (relationshipStatus === RelationshipStatus.Friends) {
        await unfollowUser(userId);
        relationshipStatus = RelationshipStatus.FollowBack;
      }
      await invalidateAll();
    } catch (err) {
      console.error("Failed to update follow status", err);
    } finally {
      loading = false;
    }
  }
</script>

<button
  type="button"
  disabled={loading}
  onclick={handleFollowClick}
  aria-label={relationshipStatus === RelationshipStatus.Friends ||
  relationshipStatus === RelationshipStatus.Following
    ? "Unfollow"
    : "Follow"}
  class="{sizeClasses[
    size
  ]} border group inline-flex secondary-button items-center gap-2 disabled:cursor-pointer {className}
  {relationshipStatus === RelationshipStatus.Friends
    ? 'bg-brand text-surface border-brand'
    : relationshipStatus === RelationshipStatus.Following
      ? 'bg-surface text-brand border-surface-alt'
      : 'bg-surface text-brand border-brand hover:bg-brand hover:text-surface'}"
>
  {#if relationshipStatus === RelationshipStatus.Friends || relationshipStatus === RelationshipStatus.Following}
    {#if relationshipStatus === RelationshipStatus.Friends}
      <span class="text-surface group-hover:hidden">
        <HeartIcon size={iconRemSize[size]} />
      </span>
      <span class="hidden text-surface group-hover:flex">
        <Minus size={iconRemSize[size]} />
      </span>
    {:else}
      <span class="flex text-brand">
        <Minus size={iconRemSize[size]} />
      </span>
    {/if}
    <span class="group-hover:hidden">{statusLabel[relationshipStatus]}</span>
    <span class="hidden group-hover:inline">Unfollow</span>
  {:else}
    <span class="flex text-brand group-hover:text-surface">
      <Plus size={iconRemSize[size]} />
    </span>
    {statusLabel[relationshipStatus]}
  {/if}
</button>
