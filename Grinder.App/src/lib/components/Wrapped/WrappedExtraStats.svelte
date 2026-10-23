<script lang="ts">
  interface Props {
    rank: number;
    label: string;
    value: number;
    message: string;
    rateLine: string | null;
    otherHighlights: { label: string; value: number }[];
  }

  let { rank, label, value, message, rateLine, otherHighlights }: Props = $props();
</script>

<div>
  <p class="text-sm font-semibold uppercase tracking-[0.2em]">
    {rank === 1 ? "your top stat" : `top stat #${rank}`}
  </p>
  <h1 class="mt-6 text-6xl md:text-left md:text-8xl text-surface">{value}</h1>
  <p class="mt-2 text-lg">{label}</p>
  {#if rateLine}
    <p class="mt-1 text-base text-surface/70">{rateLine}</p>
  {/if}
  <p class="mt-6 font-serif text-2xl italic md:text-3xl">{message}</p>

  {#if otherHighlights.length > 0}
    <div class="mt-14 border-t border-surface pt-10">
      <p class="text-sm font-semibold uppercase tracking-[0.2em]">also this year</p>
      <ul class="mt-5 space-y-2 text-lg">
        {#each otherHighlights as h (h.label)}
          <li class="flex items-baseline justify-between gap-6">
            <span>{h.label}</span>
            <span class="font-serif text-2xl font-bold tabular-nums">{h.value}</span>
          </li>
        {/each}
      </ul>
    </div>
  {/if}
</div>