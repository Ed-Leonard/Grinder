<script>
  let { error = "" } = $props();
</script>

<div
  class="mb-4 rounded-lg border border-red-500 bg-surface-alt px-4 py-3 text-sm text-red-500 alert-flash"
  role="alert"
>
  {error}
</div>
