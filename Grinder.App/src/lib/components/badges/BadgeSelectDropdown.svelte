<script lang="ts">
  import { fetchBadges, selectBadge, type Badge } from "$lib/api/badge";
  import BadgeIcon from "$lib/components/svgs/badges/BadgeIcon.svelte";
  import SelectBadgeIcon from "$lib/components/svgs/badges/select-badge.svelte";
  import { tooltip } from "$lib/utils/tooltip";

  let {
    selectedBadgeName = null,
    selectedBadgeIcon = null,
    size = 1.25,
    onSelect,
  }: {
    selectedBadgeName?: string | null;
    selectedBadgeIcon?: string | null;
    size?: number;
    onSelect?: (badge: Badge | null) => void;
  } = $props();

  let open = $state(false);
  let loading = $state(false);
  let selecting = $state(false);
  let badges = $state<Badge[] | null>(null);
  let containerEl = $state<HTMLDivElement>();

  const unlockedBadges = $derived(badges?.filter((b) => b.isUnlocked) ?? []);

  async function toggleOpen() {
    open = !open;
    if (open && badges === null) {
      loading = true;
      try {
        const response = await fetchBadges();
        if (response.ok) {
          badges = await response.json();
        }
      } finally {
        loading = false;
      }
    }
  }

  async function handlePick(badge: Badge | null) {
    if (selecting) return;
    if (badge && badge.name === selectedBadgeName) {
      open = false;
      return;
    }
    selecting = true;
    try {
      const response = await selectBadge(badge?.id ?? null);
      if (response.ok) {
        onSelect?.(badge);
        open = false;
      }
    } finally {
      selecting = false;
    }
  }

  function handleClickOutside(e: MouseEvent) {
    if (containerEl && !containerEl.contains(e.target as Node)) open = false;
  }

  $effect(() => {
    if (!open) return;
    document.addEventListener("click", handleClickOutside);
    return () => document.removeEventListener("click", handleClickOutside);
  });
</script>

<div class="relative inline-block" bind:this={containerEl}>
  <button
    type="button"
    class="icon-button z-10 {open ? 'bg-brand text-surface!' : 'text-text'}"
    aria-label="Select a badge"
    use:tooltip={"Select a badge"}
    onclick={toggleOpen}
  >
    {#if selectedBadgeIcon}
      <BadgeIcon icon={selectedBadgeIcon} {size} />
    {:else}
      <SelectBadgeIcon {size} />
    {/if}
  </button>

  {#if open}
    <div
      class="absolute left-0 z-50 mt-2 w-56 rounded-lg border border-surface-alt bg-surface shadow-sm py-1"
    >
      {#if loading}
        <p class="px-3 py-2 text-xs text-text-muted">Loading badges…</p>
      {:else if unlockedBadges.length === 0}
        <p class="px-3 py-2 text-xs text-text-muted">
          Work harder to unlock some badges
        </p>
      {:else}
        <button
          type="button"
          class="flex w-full items-center gap-2 px-3 py-2 text-sm text-left hover:bg-surface-alt disabled:opacity-50 {selectedBadgeName ===
          null
            ? 'font-semibold text-brand'
            : ''}"
          disabled={selecting}
          onclick={() => handlePick(null)}
        >
          <span
            class="flex h-5 w-5 shrink-0 items-center justify-center text-text-muted"
          >
            —
          </span>
          <span class="truncate">No status</span>
        </button>
        <div class="my-1 border-t border-surface-alt"></div>
        {#each unlockedBadges as badge (badge.id)}
          <button
            type="button"
            class="flex w-full items-center gap-2 px-3 py-2 text-sm text-left hover:bg-surface-alt disabled:opacity-50 {badge.name ===
            selectedBadgeName
              ? 'font-semibold text-brand'
              : ''}"
            disabled={selecting}
            onclick={() => handlePick(badge)}
          >
            <BadgeIcon icon={badge.icon} size={1.25} />
            <span class="truncate">{badge.name}</span>
          </button>
        {/each}
      {/if}
    </div>
  {/if}
</div>
