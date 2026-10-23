<script lang="ts">
  let {
    size = 1,
    muted = false,
  }: {
    size?: number;
    muted?: boolean;
  } = $props();
</script>

<div style="width: {size}rem; height: {size}rem;">
  <svg viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
    <path
      d="M11 5L6 9H2V15H6L11 19V5Z"
      stroke="currentColor"
      stroke-width="2"
      stroke-linecap="round"
      stroke-linejoin="round"
    ></path>
    {#if muted}
      <path
        d="M23 9L17 15M17 9L23 15"
        stroke="currentColor"
        stroke-width="2"
        stroke-linecap="round"
        stroke-linejoin="round"
      ></path>
    {:else}
      <path
        d="M15.54 8.46A5 5 0 0117 12A5 5 0 0115.54 15.54M19.07 4.93A10 10 0 0121 12A10 10 0 0119.07 19.07"
        stroke="currentColor"
        stroke-width="2"
        stroke-linecap="round"
        stroke-linejoin="round"
      ></path>
    {/if}
  </svg>
</div>
