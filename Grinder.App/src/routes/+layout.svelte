<script lang="ts">
  import "../app.css";
  import { user } from "$lib/stores/auth";
  import Toaster from "$lib/components/toasts/Toaster.svelte";

  interface Props {
    data: {
      user: {
        id: number;
        displayName: string;
        email: string;
        country: string;
      } | null;
    };
    children?: import("svelte").Snippet;
  }

  let { data, children }: Props = $props();

  // this sets it globally as a store
  $effect(() => user.set(data.user));
</script>

<div class="min-h-dvh font-sans antialiased">
  <Toaster />
  <div class="min-h-dvh bg-surface font-sans antialiased">
    {@render children?.()}
  </div>
</div>
