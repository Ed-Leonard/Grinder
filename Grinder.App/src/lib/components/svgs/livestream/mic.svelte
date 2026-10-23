<script lang="ts">
    let {
      size = 1,
    }: {
      size?: number;
    } = $props();
  </script>
  
  <div style="width: {size}rem; height: {size}rem;">
    <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" class="lucide lucide-mic preview-icon h-full w-full"><path d="M12 19v3"/><path d="M19 10v2a7 7 0 0 1-14 0v-2"/><rect x="9" y="2" width="6" height="13" rx="3"/></svg>
  </div>
  