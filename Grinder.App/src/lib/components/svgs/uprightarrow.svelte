<script lang="ts">
  let {
    size = 1,
  }: {
    size?: number;
  } = $props();
</script>

<div style="width: {size}rem; height: {size}rem;">
  <svg xmlns="http://www.w3.org/2000/svg" width="100%" height="100%" viewBox="0 0 24 24" fill="none" stroke="currentColor" 
  stroke-width="2" 
  stroke-linecap="round" 
  stroke-linejoin="round" 
  class="lucide lucide-arrow-up-right">
  <path d="M7 7h10v10"/><path d="M7 17 17 7"/>
  </svg>
</div>
