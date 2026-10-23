<script lang="ts">
  import type { Reaction, ReactionResponse } from "$lib/api/reaction";
  import { resolve } from "$app/paths";
  import { tooltip } from "$lib/utils/tooltip";

  let {
    reactions,
    onToggle,
  }: {
    reactions: ReactionResponse[];
    onToggle: (reaction: Reaction) => void;
  } = $props();
</script>

{#each reactions as r (r.reaction.name)}
  <button
    type="button"
    use:tooltip={`:${r.reaction.name}:`}
    onclick={() => onToggle(r.reaction)}
    class="flex items-center gap-1 rounded-full border px-2 py-0.5 text-xs cursor-pointer z-10 -ml-1
    {r.reactedByCurrentUser
      ? 'border-brand bg-surface-alt text-text-muted hover:bg-surface-alt-hover'
      : 'border-surface-alt-hover bg-surface-alt text-text-muted hover:bg-surface-alt-hover'}
    "
  >
    <img
      src={resolve(`/reactions/${r.reaction.fileName}` as any)}
      alt={r.reaction.name}
      loading="lazy"
      class="pointer-events-none h-6 w-6 object-contain"
    />
    <span>{r.count}</span>
  </button>
{/each}
