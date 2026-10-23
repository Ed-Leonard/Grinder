<script lang="ts">
  import { resolve } from "$app/paths";
  import type { Reaction } from "$lib/api/reaction";

  let {
    content = "",
    reactionsByName,
    imgClass = "h-8 w-8",
  }: {
    content?: string;
    reactionsByName: Map<string, Reaction>;
    imgClass?: string;
  } = $props();

  type Part =
    | { kind: "text"; value: string }
    | { kind: "reaction"; reaction: Reaction };

  let parts = $derived.by(() => {
    const result: Part[] = [];
    const pattern = /:([^:\s]+):/g;
    let last = 0;
    let match: RegExpExecArray | null;

    while ((match = pattern.exec(content)) !== null) {
      const reaction = reactionsByName.get(match[1]);
      if (!reaction) {
        // Not a known reaction: resume right after the opening colon so the
        // closing colon can still open the next candidate
        pattern.lastIndex = match.index + 1;
        continue;
      }
      if (match.index > last) {
        result.push({ kind: "text", value: content.slice(last, match.index) });
      }
      result.push({ kind: "reaction", reaction });
      last = match.index + match[0].length;
    }
    if (last < content.length) {
      result.push({ kind: "text", value: content.slice(last) });
    }
    return result;
  });
</script>

{#each parts as part, i (i)}
  {#if part.kind === "text"}
    {part.value}
  {:else}
    <img
      src={resolve(`/reactions/${part.reaction.fileName}` as any)}
      alt={`:${part.reaction.name}:`}
      title={`:${part.reaction.name}:`}
      class="inline-block align-middle object-contain {imgClass}"
    />
  {/if}
{/each}

