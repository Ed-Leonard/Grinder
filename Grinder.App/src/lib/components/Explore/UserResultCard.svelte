<script lang="ts">
  import { resolve } from "$app/paths";
  import ProfileIcon from "$lib/components/svgs/profileicon.svelte";
  import FollowButton from "$lib/components/Friends/FollowButton.svelte";
  import type { SearchResponse } from "$lib/types";

  type SearchUser = NonNullable<SearchResponse["users"]>[number];

  let { user }: { user: SearchUser } = $props();
</script>

<a
  href={resolve(`/profile/${user.id}`)}
  class="relative w-full inline-flex items-center justify-between gap-4 secondary-button bg-surface hover:bg-surface-alt-hover rounded-none my-0 md:rounded-full"
>
  <ProfileIcon size={2.5} userId={user.id} clickable={false} />

  <div class="flex-1 min-w-0">
    <p class="text-base font-medium truncate group-hover:underline">
      {user.displayName}
    </p>
  </div>

  <FollowButton
    userId={user.id}
    bind:relationshipStatus={user.relationshipStatus}
    size="sm"
  />
</a>