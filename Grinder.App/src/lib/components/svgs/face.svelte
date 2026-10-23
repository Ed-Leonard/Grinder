<script lang="ts">
  let {
    size = 1.5,
  }: {
    size?: number;
  } = $props();
</script>

<div style="width: {size}rem; height: {size}rem;">
  <svg
    xmlns="http://www.w3.org/2000/svg"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-linecap="round"
    stroke-linejoin="round"
    id="Smile-Plus--Streamline-Lucide"
  >
    <path d="M22 11v1a10 10 0 1 1 -9 -10" stroke-width="2"></path>
    <path d="M8 14s1.5 2 4 2 4 -2 4 -2" stroke-width="2"></path>
    <path d="m9 9 0.01 0" stroke-width="2"></path>
    <path d="m15 9 0.01 0" stroke-width="2"></path>
    <path d="M16 5h6" stroke-width="2"></path>
    <path d="M19 2v6" stroke-width="2"></path>
  </svg>
</div>
