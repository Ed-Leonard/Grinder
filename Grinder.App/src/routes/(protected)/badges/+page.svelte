<script lang="ts">
  import { resolve } from "$app/paths";
  import BadgeIcon from "$lib/components/svgs/badges/BadgeIcon.svelte";
  import LeftArrow from "$lib/components/svgs/leftarrow.svelte";
  import { fetchBadges, selectBadge, type Badge } from "$lib/api/badge";
  import { getUserProfile } from "$lib/api/profile";
  import { user } from "$lib/stores/auth";
  import BasePage from "$lib/components/utils/BasePage.svelte";
  import { tooltip } from "$lib/utils/tooltip";

  let badges = $state<Badge[]>([]);
  let selectedBadgeName = $state<string | null>(null);
  let loading = $state(true);
  let loadError = $state(false);
  let selectingId = $state<number | null>(null);
  let loadedUserId: number | null = null;

  let earnedCount = $derived(badges.filter((b) => b.isUnlocked).length);

  async function loadBadges(userId: number) {
    loading = true;
    loadError = false;
    try {
      const [badgesResponse, profileResponse] = await Promise.all([
        fetchBadges(),
        getUserProfile(userId),
      ]);

      if (!badgesResponse.ok || !profileResponse.ok) {
        loadError = true;
        return;
      }

      badges = await badgesResponse.json();
      const profile = await profileResponse.json();
      selectedBadgeName = profile.selectedBadgeName ?? null;
    } catch {
      loadError = true;
    } finally {
      loading = false;
    }
  }

  $effect(() => {
    const currentId = $user?.id ?? null;
    if (currentId !== null && currentId !== loadedUserId) {
      loadedUserId = currentId;
      loadBadges(currentId);
    }
  });

  function progressPercent(badge: Badge): number {
    if (badge.threshold <= 0) return 0;
    return Math.min(100, Math.round((badge.progress / badge.threshold) * 100));
  }

  async function handleToggle(badge: Badge) {
    if (!badge.isUnlocked || selectingId !== null) return;

    const isSelected = badge.name === selectedBadgeName;
    selectingId = badge.id;
    try {
      const response = await selectBadge(isSelected ? null : badge.id);
      if (response.ok) {
        selectedBadgeName = isSelected ? null : badge.name;
      }
    } finally {
      selectingId = null;
    }
  }
</script>

<BasePage maxWidth="max-w-7xl" class="items-start! p-4 md:p-8">
  <div>
    <div class="mb-6 flex items-center gap-4">
      <a
        class="icon-button shrink-0"
        use:tooltip={"Back to profile"}
        aria-label="Back to profile"
        href={resolve(`/profile/${$user?.id ?? ""}` as any)}
      >
        <LeftArrow size={1.5} />
      </a>
      <div>
        <h1 class="text-3xl font-bold pt-5">Badges</h1>
        {#if !loading && !loadError}
          <p class="text-sm text-text-muted">
            {earnedCount} of {badges.length} earned
          </p>
        {/if}
      </div>
    </div>

    {#if loadError}
      <p class="text-sm text-text-muted">Failed to load badges.</p>
    {:else if loading}
      <p class="text-sm text-text-muted">Loading badges…</p>
    {:else}
      <!-- Badge grid -->
      <div
        class="grid w-full grid-cols-1 xs:grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 pt-6"
      >
        {#each badges as badge}
          <button
            type="button"
            disabled={!badge.isUnlocked || selectingId !== null}
            aria-label={badge.isUnlocked
              ? `${badge.name === selectedBadgeName ? "Deselect" : "Select"} ${badge.name} badge`
              : `${badge.name} badge (locked)`}
            onclick={() => handleToggle(badge)}
            class="flex flex-col items-center gap-3 rounded-lg border bg-surface p-4 shadow-sm transition-transform disabled:cursor-auto enabled:hover:scale-105 select-none disabled:opacity-40 disabled:grayscale {badge.name ===
            selectedBadgeName
              ? 'border-brand ring-2 ring-brand'
              : 'border-surface-alt'}"
          >
            <span class="text-center text-lg font-semibold">{badge.name}</span>
            <BadgeIcon icon={badge.icon} size={5} />
            <p class="text-center text-xs text-text-muted">
              {badge.description}
            </p>
            {#if badge.isUnlocked && badge.earnedAt}
              <span class="mt-auto text-xs text-text font-medium">
                Earned {new Date(badge.earnedAt).toLocaleDateString()}
              </span>
            {:else}
              <div class="mt-auto w-full">
                <div class="h-1.5 w-full rounded-full bg-surface-alt">
                  <div
                    class="h-1.5 rounded-full bg-brand transition-all"
                    style="width: {progressPercent(badge)}%"
                  ></div>
                </div>
                <span class="mt-1 block text-center text-xs text-text-muted">
                  {badge.progress} / {badge.threshold}
                </span>
              </div>
            {/if}
          </button>
        {/each}
      </div>
    {/if}
  </div>
</BasePage>
