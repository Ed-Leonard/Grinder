<script lang="ts">
  let {
    size = 1,
  }: {
    size?: number;
    colour?: string;
  } = $props();
</script>

<div style="width: {size}rem; height: {size}rem;">
  <svg xmlns="http://www.w3.org/2000/svg" width="100%" height="100%" 
    viewBox="0 0 24 24" 
    fill="none" 
    stroke="currentColor" 
    stroke-width="2" 
    stroke-linecap="round" 
    stroke-linejoin="round" 
    class="lucide lucide-play">
    <path d="M5 5a2 2 0 0 1 3.008-1.728l11.997 6.998a2 2 0 0 1 .003 3.458l-12 7A2 2 0 0 1 5 19z"/>
  </svg>
</div>