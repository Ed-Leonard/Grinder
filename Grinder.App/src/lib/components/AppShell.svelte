<script lang="ts">
  import Navbar from "$lib/components/Navbar.svelte";
  import NotificationBell from "$lib/components/NotificationBell.svelte";
  import HamburgerMenu from "$lib/components/svgs/hamburgermenu.svelte";
  import ThemeToggle from "$lib/components/svgs/theme.svelte";
  import { resolve } from "$app/paths";
  import { onMount } from "svelte";
  import { theme } from "$lib/stores/theme";
  import { notificationStore } from "$lib/stores/notifications";
  import { connectNotificationHub } from "$lib/hubs/notificationHub";
  import { fetchNotifications, fetchUnreadCount } from "$lib/api/notifications";
  import { tooltip } from "$lib/utils/tooltip";

  interface Props {
    children?: import("svelte").Snippet;
  }

  let { children }: Props = $props();
  let navOpen = $state(true);

  onMount(async () => {
    try {
      const [page, unread] = await Promise.all([
        fetchNotifications(),
        fetchUnreadCount(),
      ]);
      notificationStore.setInitial(page.items, unread);
      connectNotificationHub();
    } catch (err) {
      console.error("Failed to load notifications", err);
    }
  });
</script>

<div class="h-dvh flex flex-col overflow-hidden">
  <div
    class="inline-flex justify-between items-center gap-2 px-2 pr-4 py-4 z-10"
  >
    <div class="inline-flex gap-4 items-center">
      <button
        class="md:block hidden icon-button"
        onclick={() => (navOpen = !navOpen)}
        aria-label={navOpen ? "Collapse side bar" : "Open side bar"}
        use:tooltip={navOpen ? "Collapse side bar" : "Open side bar"}
      >
        <HamburgerMenu size={1.5} />
      </button>
      <a href={resolve("/feed")}>
        <h1 class="hover:text-brand transition-colors">Grinder</h1>
      </a>
    </div>

    <div class="inline-flex gap-4 items-center">
      <button
        onclick={theme.toggle}
        class="icon-button"
        use:tooltip={$theme === "dark" ? "Light Mode" : "Dark Mode"}
      >
        <ThemeToggle size={1.5} dark={$theme === "dark"} />
      </button>
      <NotificationBell />
    </div>
  </div>

  <div class="flex-1 flex flex-col md:flex-row min-h-0">
    <Navbar bind:navOpen />
    <main class="order-1 md:order-2 flex-1 overflow-y-auto md:rounded-tl-lg">
      {@render children?.()}
    </main>
  </div>
</div>
