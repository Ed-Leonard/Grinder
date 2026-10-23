<script lang="ts">
  import { getUserLocale } from "$lib/utils/dateUtils.svelte";

  let {
    date,
    time,
    className = "",
  }: {
    date: string;
    time: string;
    className?: string;
  } = $props();
</script>

<span class={className}>
  {new Date(`1970-01-01T${time}`).toLocaleTimeString(getUserLocale(), {
    hour: "2-digit",
    minute: "2-digit",
  })}
  &middot;
  {new Date(`${date}T00:00:00`).toLocaleDateString(getUserLocale(), {
    year: "2-digit",
    month: "numeric",
    day: "numeric",
  })}
</span>
