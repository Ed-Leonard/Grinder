<script lang="ts">
  import MessageIcon from "$lib/components/svgs/message.svelte";
</script>

<section
  class="hidden md:flex flex-1 min-w-0 min-h-0 flex-col items-center justify-center"
>
  <div class="text-center gap-2 items-center flex flex-col">
    <MessageIcon size={3} />
    <h2>Your messages</h2>
    <p class="text-sm text-text-muted max-w-xs">
      Send a private message to a friend.
    </p>
  </div>
</section>
