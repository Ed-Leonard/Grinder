<script lang="ts">
  import AppShell from "$lib/components/AppShell.svelte";
  import LeftArrow from "$lib/components/svgs/leftarrow.svelte";
  import { resolve } from "$app/paths";
  import { onMount } from "svelte";
  import { user, checkAuth } from "$lib/stores/auth";

  interface Props {
    children?: import("svelte").Snippet;
  }

  let { children }: Props = $props();
  let authReady = $state(false);

  onMount(async () => {
    await checkAuth();
    authReady = true;
  });
</script>

{#if $user}
  <AppShell>
    {@render children?.()}
  </AppShell>
{:else if authReady}
  <div class="min-h-dvh bg-surface">
    <header
      class="sticky top-0 z-10 border-b border-surface-alt bg-surface/80 backdrop-blur"
    >
      <div class="mx-auto flex max-w-3xl items-center gap-3 px-4 py-4">
        <a
          href={resolve("/")}
          class="icon-button -ml-2"
          aria-label="Back to home"
        >
          <LeftArrow size={1.25} />
        </a>
        <span class="text-sm font-semibold text-text-muted">Grinder</span>
      </div>
    </header>
    {@render children?.()}
  </div>
{:else}
  <div class="min-h-dvh bg-surface"></div>
{/if}
