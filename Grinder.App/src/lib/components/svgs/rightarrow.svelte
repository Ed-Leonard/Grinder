<script lang="ts">
  let {
    size = 1,
  }: {
    size?: number;
  } = $props();
</script>

<div style="width: {size}rem; height: {size}rem;" class="rotate-180">
  <svg viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
    <path
      d="M19 12H5M11 18L5 12L11 6"
      stroke="currentColor"
      stroke-width="2"
      stroke-linecap="round"
      stroke-linejoin="round"
    ></path>
  </svg>
</div>
