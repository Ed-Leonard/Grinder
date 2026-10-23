<script lang="ts">
  import AppShell from "$lib/components/AppShell.svelte";
  import { user, checkAuth } from "$lib/stores/auth";
  import { goto } from "$app/navigation";
  import { resolve } from "$app/paths";
  import { get } from "svelte/store";
  import { onDestroy, onMount } from "svelte";
  import { fetchWrappedStatus, dismissWrapped } from "$lib/api/wrapped";
  import WrappedModal from "$lib/components/Wrapped/WrappedModal.svelte";
  import WrappedPrompt from "$lib/components/Wrapped/WrappedPrompt.svelte";
  import { startChat, stopChat } from "$lib/hubs/chatHub";
  import { stopNotifications } from "$lib/hubs/notificationHub";

  let showWrappedPrompt = $state(false);
  let showWrappedPopup = $state(false);
  let wrappedYear = $state<number | null>(null);

  interface Props {
    children?: import("svelte").Snippet;
  }

  let { children }: Props = $props();

  onMount(async () => {
    await checkAuth();
    if (!get(user)) {
      goto(resolve("/login"));
      return;
    }
    startChat().catch((err) => console.error("Chat connection failed", err));

    const { currentEligibleYear, dismissed } = await fetchWrappedStatus();
    if (currentEligibleYear !== null) {
      wrappedYear = currentEligibleYear;
      if (!dismissed) {
        showWrappedPrompt = true;
      }
    }
  });

  onDestroy(() => {
    stopChat().catch(() => {});
    stopNotifications().catch(() => {});
  });

  async function handlePromptAccept() {
    showWrappedPrompt = false;
    showWrappedPopup = true;
    if (wrappedYear !== null) {
      await dismissWrapped(wrappedYear);
    }
  }

  async function handlePromptDismiss() {
    showWrappedPrompt = false;
    if (wrappedYear !== null) {
      await dismissWrapped(wrappedYear);
    }
  }

  function handleWrappedClose() {
    showWrappedPopup = false;
  }
</script>

{#if showWrappedPrompt}
  <WrappedPrompt
    open={showWrappedPrompt}
    year={wrappedYear}
    onview={handlePromptAccept}
    ondismiss={handlePromptDismiss}
  />
{/if}
{#if showWrappedPopup && wrappedYear !== null}
  <WrappedModal open={showWrappedPopup} year={wrappedYear} onclose={handleWrappedClose} />
{/if}
<AppShell>
  {@render children?.()}
</AppShell>
