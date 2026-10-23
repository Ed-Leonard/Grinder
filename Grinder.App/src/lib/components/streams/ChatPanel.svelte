<script lang="ts">
  import Chat from "$lib/components/streams/Chat.svelte";
  import Xicon from "$lib/components/svgs/xicon.svelte";
  import { tooltip } from "$lib/utils/tooltip";

  let {
    roomName,
    isOpen,
    onClose,
  }: {
    roomName: string;
    isOpen: boolean;
    onClose: () => void;
  } = $props();

  let hasOpened = $state(false);

  $effect(() => {
    if (isOpen) hasOpened = true;
  });
</script>

{#if hasOpened}
  <!--help from claude for below styles (styled to work with current live page 20/09/26 may be able to be simplified)-->
  <div
    class="relative overflow-hidden overflow-y-auto bg-surface shrink-0 shadow-sm
           w-full lg:h-full md:rounded-b-lg lg:rounded-lg
           transition-[height,width] duration-300 ease-[cubic-bezier(0.33,1,0.68,1)]
           {isOpen ? 'h-72 lg:w-lg' : 'h-0 lg:w-0'}"
    inert={!isOpen}
  >
    <div
      class="absolute left-0 right-0 bottom-0 h-72
             lg:left-auto lg:right-0 lg:top-0 lg:bottom-auto lg:h-full lg:w-lg
             bg-surface flex flex-col md:rounded-b-lg lg:rounded-lg"
    >
      <div
        class="flex items-center justify-between border-b border-surface-alt p-2 shrink-0"
      >
        <h2 class="ml-2">Chat</h2>

        <button
          type="button"
          aria-label="Close chat"
          use:tooltip={"Close chat"}
          class="icon-button"
          onclick={onClose}
        >
          <Xicon />
        </button>
      </div>
      <div class="flex-1 min-h-0 overflow-hidden">
        <Chat {roomName} />
      </div>
    </div>
  </div>
{/if}

