<script lang="ts">
  import { user, checkAuth } from "$lib/stores/auth";
  import { goto } from "$app/navigation";
  import { resolve } from "$app/paths";
  import { get } from "svelte/store";
  import { onMount } from "svelte";

  interface Props {
    children?: import("svelte").Snippet;
  }
  let { children }: Props = $props();

  onMount(async () => {
    await checkAuth();
    const currentUser = get(user);
    if (currentUser) {
      goto(resolve(`/profile/${currentUser.id}`));
    }
  });
</script>

{@render children?.()}
