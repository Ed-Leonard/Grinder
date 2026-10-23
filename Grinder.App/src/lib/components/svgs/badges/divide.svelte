<script lang="ts">
  let {
    size = 1,
    colour = "#22c55e",
  }: {
    size?: number;
    colour?: string;
  } = $props();
</script>

<div style="width: {size}rem; height: {size}rem;">
  <svg
    xmlns="http://www.w3.org/2000/svg"
    viewBox="0 0 24 24"
    fill="none"
    stroke={colour}
    stroke-width="2"
    stroke-linecap="round"
    stroke-linejoin="round"
  >
    <path d="M16 3h5v5" />
    <path d="M8 3H3v5" />
    <path d="M12 22v-8.3a4 4 0 0 0-1.172-2.872L3 3" />
    <path d="m15 9 6-6" />
  </svg>
</div>
