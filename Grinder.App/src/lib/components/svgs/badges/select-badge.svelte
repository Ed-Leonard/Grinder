<script lang="ts">
  let {
    size = 1,
  }: {
    size?: number;
  } = $props();
</script>

<div style="width: {size}rem; height: {size}rem;">
  <svg
    viewBox="0 0 24 24"
    fill="none"
    xmlns="http://www.w3.org/2000/svg"
    stroke="currentColor"
    stroke-width="2"
    stroke-linecap="round"
    stroke-linejoin="round"
  >
    <path
      d="m15.477 12.89 1.515 8.526a.5.5 0 0 1-.81.47l-3.58-2.687a1 1 0 0 0-1.197 0l-3.586 2.686a.5.5 0 0 1-.81-.469l1.514-8.526"
    />
    <circle cx="12" cy="8" r="6" />
    <path d="M12 5.5V10.5M9.5 8H14.5" />
  </svg>
</div>
