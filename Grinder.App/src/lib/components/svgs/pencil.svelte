<script lang="ts">
  let {
    size = 1.125,
  }: {
    size?: number;
  } = $props();
</script>

<svg
  xmlns="http://www.w3.org/2000/svg"
  style="width: {size}rem; height: {size}rem;"
  viewBox="0 0 24 24"
  fill="none"
  stroke="currentColor"
  stroke-width="2"
  stroke-linecap="round"
  stroke-linejoin="round"
>
  <path d="M17 3a2.828 2.828 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5L17 3z" />
</svg>
