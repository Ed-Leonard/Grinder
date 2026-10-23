<script lang="ts">
  import Todo from "$lib/components/svgs/todo.svelte";
  import Clock from "$lib/components/svgs/clock.svelte";
  import Done from "$lib/components/svgs/done.svelte";

  let {
    status = 0,
    size = 1,
    className = "",
    colour,
    invert = false,
  }: {
    status: number;
    size?: number;
    className?: string;
    colour?: string;
    invert?: boolean;
  } = $props();
</script>

<span class="shrink-0 {className}">
  {#if status == 0}
    <Todo {size} {colour} {invert} />
  {:else if status == 1}
    <Clock {size} {colour} {invert} />
  {:else}
    <Done {size} {colour} {invert} />
  {/if}
</span>
