<script lang="ts" generics="T">
  let {
    loading = false,
    submitted = false,
    required = false,
    value = $bindable(""),
    items,
    id,
    label = "",
    emptyLabel = "Select an item",
    getValue = (item: T) => item as unknown as string,
    getLabel = (item: T) => item as unknown as string,
    onchange,
    class: className = "",
  }: {
    id: string;
    loading?: boolean;
    submitted: boolean;
    required?: boolean;
    value?: string;
    items: T[];
    label?: string;
    emptyLabel?: string;
    getValue?: (item: T) => string;
    getLabel?: (item: T) => string;
    onchange?: (event: Event) => void;
    class?: string;
  } = $props();
</script>

<div class="floating relative {className}">
  <select
    class="input-field peer pl-5 cursor-pointer appearance-none pr-10 {submitted
      ? 'submitted'
      : ''}"
    id={`select-${id}`}
    placeholder="Select"
    disabled={loading}
    {required}
    bind:value
    {onchange}
  >
    <option selected value={""}>{emptyLabel}</option>
    {#each items as item}
      <option value={getValue(item)}>{getLabel(item)}</option>
    {/each}
  </select>
  <label for="select" class="input-label"> {label} </label>
  <svg
    class="pointer-events-none absolute right-4 top-[50%] -translate-y-1/2 text-text-muted"
    xmlns="http://www.w3.org/2000/svg"
    width="18"
    height="18"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-width="2"
    stroke-linecap="round"
    stroke-linejoin="round"
  >
    <path d="m6 9 6 6 6-6" />
  </svg>
  <p class="input-invalid">Please select an option</p>
</div>
