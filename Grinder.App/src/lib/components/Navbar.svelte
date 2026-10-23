<script lang="ts">
  import { goto } from "$app/navigation";
  import { resolve } from "$app/paths";
  import { page } from "$app/state";
  import ProfileIcon from "$lib/components/svgs/profileicon.svelte";
  import HomeIcon from "$lib/components/svgs/home.svelte";
  import ClipIcon from "$lib/components/svgs/clips/clip.svelte";
  import type { Component } from "svelte";
  import { user } from "$lib/stores/auth";
  import Magnifying from "./svgs/magnifying.svelte";
  import MessageIcon from "./svgs/message.svelte";
  import Pluscircle from "$lib/components/svgs/pluscircle.svelte";
  import Broadcast from "./svgs/broadcast.svelte";

  let { navOpen = $bindable(true) }: { navOpen: boolean } = $props();

  let buttons: {
    icon: Component;
    label: string;
    route: string;
    props?: Record<string, unknown>;
  }[] = $derived([
    {
      icon: HomeIcon,
      label: "Feed",
      route: resolve("/feed"),
      props: { size: 1.5 },
    },
    {
      icon: Magnifying,
      label: "Explore",
      route: resolve("/explore"),
      props: { size: 1.5 },
    },
    {
      icon: ClipIcon,
      label: "Watch",
      route: resolve("/watch"),
      props: { size: 1.5 },
    },
    {
      icon: Pluscircle,
      label: "Create",
      route: resolve("/tasks/new"),
      props: { size: 1.5 },
    },

    {
      icon: Broadcast,
      label: "Stream",
      route: resolve("/livestream/start"),
      props: { size: 1.5 },
    },
    {
      icon: MessageIcon,
      label: "Messages",
      route: resolve("/chat"),
      props: { size: 1.5 },
    },
  ]);
</script>

<div
  class="w-full order-2 md:order-1 md:h-full md:w-auto flex flex-row md:flex-col"
>
  <nav
    class="flex-1 flex items-center min-h-0 transition-[width] duration-300 ease-in-out mb-2 md:mb-0 {navOpen
      ? 'md:w-63.5'
      : 'md:w-14'}"
  >
    <div class="w-full h-full flex flex-row md:flex-col px-2 gap-2">
      <!-- Nav buttons -->
      <div
        class="inline-flex md:flex-col gap-2 justify-between w-full md:justify-start"
      >
        {#each buttons as { icon: Icon, label, route, props }}
          {@const isActive =
            page.url.pathname === route ||
            page.url.pathname.startsWith(route + "/")}
          <a
            class="flex justify-center md:justify-start md:grid md:grid-cols-[2rem_1fr] items-center gap-3 rounded-full p-1 py-3 md:py-2 w-full transition-colors {isActive
              ? 'bg-brand'
              : 'hover:bg-surface-alt-hover'}"
            href={route}
            aria-current={isActive ? "page" : undefined}
          >
            <div
              class="flex items-center justify-center transition-colors duration-300 {isActive
                ? 'text-surface'
                : ''}"
            >
              <Icon {...props} />
            </div>
            <span
              class="text-left font-semibold hidden md:inline-block transition pointer-events-none {isActive
                ? 'text-surface'
                : ''} {navOpen ? 'opacity-100 delay-100' : 'opacity-0'} "
              >{label}</span
            >
          </a>
        {/each}
      </div>

      <!-- Note that the w-1/4 aligns its size for mobile viewing and should be updated according to 1/[length of buttons array] -->
      <div class="mt-auto relative w-1/7 md:w-full md:pb-2">
        {#if $user}
          <hr class="mb-3 md:block hidden text-surface-alt-hover" />
          {@const isProfileActive =
            page.url.pathname === resolve("/profile") ||
            page.url.pathname.startsWith(resolve("/profile/"))}
          <button
            class="flex justify-center md:justify-start md:grid md:grid-cols-[2rem_1fr] items-center gap-3 p-1 py-3 md:py-2 rounded-full w-full transition-colors
    {isProfileActive ? 'bg-brand' : 'hover:bg-surface-alt-hover'}"
            onclick={() => {
              goto(resolve(`/profile/${$user.id}`));
            }}
          >
            <div
              class="flex items-center justify-center transition-colors duration-300 {isProfileActive
                ? 'text-surface'
                : ''}"
            >
              <ProfileIcon size={1.5} userId={$user.id} clickable={false} />
            </div>
            <span
              class="text-left font-semibold hidden md:inline-block pointer-events-none transition {isProfileActive
                ? 'text-surface'
                : ''} {navOpen
                ? 'opacity-0 md:opacity-100 delay-100'
                : 'opacity-0'} ">Profile</span
            >
          </button>
        {/if}
      </div>
    </div>
  </nav>
</div>
