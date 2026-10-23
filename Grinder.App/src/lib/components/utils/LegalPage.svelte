<script lang="ts">
  import type { Snippet } from "svelte";
  import BasePage from "$lib/components/utils/BasePage.svelte";
  import { user } from "$lib/stores/auth";

  let {
    title,
    lastUpdated,
    children,
  }: {
    title: string;
    lastUpdated: string;
    children: Snippet;
  } = $props();
</script>

<svelte:head>
  <title>{title} - Grinder</title>
</svelte:head>

{#snippet body()}
  <h1>{title}</h1>
  <p class="mb-10 text-center text-xs text-text-muted">
    Last updated: {lastUpdated}
  </p>
  {@render children()}
{/snippet}

{#if $user}
  <BasePage
    maxWidth="max-w-3xl"
    class="bg-surface! md:bg-surface-alt! items-start md:p-4"
  >
    <article class="legal-content card">
      {@render body()}
    </article>
  </BasePage>
{:else}
  <article class="legal-content mx-auto max-w-3xl px-4 py-10 md:py-16">
    {@render body()}
  </article>
{/if}
