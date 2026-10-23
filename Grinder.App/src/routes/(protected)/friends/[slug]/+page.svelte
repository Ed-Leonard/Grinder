<script lang="ts">
  import BasePage from "$lib/components/utils/BasePage.svelte";
  import type { PageData } from "./$types";
  import Profileicon from "$lib/components/svgs/profileicon.svelte";
  import FollowButton from "$lib/components/Friends/FollowButton.svelte";
  import { RelationshipStatus } from "$lib/types";
  import { user } from "$lib/stores/auth";
  import { resolve } from "$app/paths";

  let { data }: { data: PageData } = $props();
</script>

<BasePage
  maxWidth="max-w-3xl"
  class="items-start md:items-center bg-surface! md:bg-surface-alt!"
>
  <div class="card flex flex-col md:gap-4 items-center p-0 md:p-8">
    <h1 class="self-start p-4">
      {data.profile.displayName}'s Friends
    </h1>
    {#each data.friends as friend (friend.id)}
      <div
        class="relative w-full inline-flex items-center justify-between gap-4 secondary-button bg-surface hover:bg-surface-hover rounded-none md:rounded-full"
      >
        <a
          href={resolve(`/profile/${friend.id}`)}
          class="inset-0 absolute z-0"
          aria-label="go to profile"
        >
        </a>
        <div class="flex items-center gap-4">
          <Profileicon userId={friend.id} size={3} />
          <div class="text-lg font-semibold">
            {friend.displayName}
          </div>
        </div>
        {#if $user && friend.id !== $user.id}
          <FollowButton
            userId={friend.id}
            bind:relationshipStatus={
              friend.relationshipStatus as RelationshipStatus
            }
            size="md"
            class="z-10"
          />
        {/if}
      </div>
    {/each}
  </div>
</BasePage>
