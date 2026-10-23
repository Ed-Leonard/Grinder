<script lang="ts">
  import Conversation from "$lib/components/chat/Conversation.svelte";
  import { page } from "$app/state";

  let otherUserId = $derived(Number(page.params.slug));
</script>

{#key otherUserId}
  <Conversation {otherUserId} />
{/key}
