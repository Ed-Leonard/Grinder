<script>
  let { success = "" } = $props();
</script>

<div
  class="mb-4 rounded-lg border border-green-400 bg-green-200 px-4 py-3 text-sm text-green-900 alert-flash"
  role="alert"
>
  {success}
</div>
