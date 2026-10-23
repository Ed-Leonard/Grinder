<script lang="ts">
  import type { Snippet } from "svelte";

  let {
    children,
    maxWidth = "max-w-lg",
    class: className = "",
  }: {
    children: Snippet;
    maxWidth?: string;
    class?: string;
  } = $props();
</script>

<div
  class="flex min-h-full items-center justify-center bg-surface-alt {className}"
>
  <div class="w-full {maxWidth}">
    {@render children()}
  </div>
</div>
