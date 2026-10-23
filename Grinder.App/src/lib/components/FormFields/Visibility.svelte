<script lang="ts">
  import { TaskVisibility } from "$lib/types";
  import Lock from "$lib/components/svgs/visibility/lock.svelte";
  import Globe from "$lib/components/svgs/visibility/globe.svelte";
  import UserGroup from "$lib/components/svgs/visibility/usergroup.svelte";

  const allValues = Object.values(TaskVisibility).filter(
    (v) => typeof v === "number",
  ) as TaskVisibility[];

  let {
    visibility = $bindable<TaskVisibility>(),
    options = allValues,
    disabledOptions = [],
  }: {
    visibility: TaskVisibility;
    options?: TaskVisibility[];
    disabledOptions?: TaskVisibility[];
  } = $props();
</script>

<div class="rounded-lg border border-surface-alt p-4 flex flex-col gap-2">
  <p class="text-sm font-medium">
    Visibility <span class="text-red-500">*</span>
  </p>
  <div class="inline-flex gap-2 flex-wrap w-full justify-center">
    {#each options as value}
      <button
        type="button"
        disabled={disabledOptions.includes(value)}
        class="secondary-button {value === visibility
          ? 'bg-brand text-surface'
          : ''}"
        onclick={() => (visibility = value)}
      >
        <div class="inline-flex gap-2 items-center">
          {#if value === TaskVisibility.Private}
            <Lock />
          {:else if value === TaskVisibility.FriendsOnly}
            <UserGroup />
          {:else}
            <Globe />
          {/if}
          {value === TaskVisibility.FriendsOnly
            ? "Friends Only"
            : TaskVisibility[value]}
        </div>
      </button>
    {/each}
  </div>
</div>
