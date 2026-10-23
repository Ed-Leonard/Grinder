<script>
  import { toasts } from "./toast.svelte.js";
</script>

<div class="fixed bottom-4 right-4 flex flex-col gap-2 z-50">
  {#each [...toasts.values()] as { message } (message)}
    <div class="bg-surface px-4 py-2 rounded-lg shadow-lg">
      {message}
    </div>
  {/each}
</div>
