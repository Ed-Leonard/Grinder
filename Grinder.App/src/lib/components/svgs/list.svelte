<script lang="ts">
  let {
    size = 1,
  }: {
    size?: number;
  } = $props();
</script>

<div style="width: {size}rem; height: {size}rem;">
  <svg viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
    <circle cx="4" cy="6" r="1.25" fill="currentColor" />
    <circle cx="4" cy="12" r="1.25" fill="currentColor" />
    <circle cx="4" cy="18" r="1.25" fill="currentColor" />
    <path
      d="M8.5 6H21M8.5 12H21M8.5 18H21"
      stroke="currentColor"
      stroke-width="2"
      stroke-linecap="round"
      stroke-linejoin="round"
    ></path>
  </svg>
</div>
