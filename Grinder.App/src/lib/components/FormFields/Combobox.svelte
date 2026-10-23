<script lang="ts" generics="T">
  import type { Snippet } from "svelte";

  let {
    items,
    submitted = false,
    value = $bindable(null),
    getId,
    getLabel,
    getError = () => "",
    placeholder = "",
    label = "",
    id = "combobox",
    allowClear = true,
    required = false,
    class: className = "input-field peer appearance-none",
    itemSnippet,
  }: {
    items: T[];
    value?: string | number | null;
    submitted?: boolean;
    getId: (item: T) => string | number;
    getLabel: (item: T) => string;
    getError?: () => string;
    placeholder?: string;
    label?: string;
    id?: string;
    allowClear?: boolean;
    required?: boolean;
    class?: string;
    itemSnippet?: Snippet<[T]>;
  } = $props();

  let query = $state("");
  let open = $state(false);
  let activeIndex = $state(-1);

  $effect(() => {
    const match = items.find((i) => getId(i) === value);
    query = match ? getLabel(match) : "";
  });

  let filtered = $derived(
    items.filter((i) =>
      getLabel(i).toLowerCase().includes(query.toLowerCase()),
    ),
  );

  function select(item: T) {
    value = getId(item);
    query = getLabel(item);
    open = false;
    activeIndex = -1;
  }

  function handleInput() {
    open = true;
    activeIndex = -1;
    if (allowClear && query === "") {
      value = null;
    }
  }

  function handleBlur() {
    setTimeout(() => {
      const match = items.find((i) => getLabel(i) === query);
      if (match) {
        value = getId(match);
        query = getLabel(match);
      } else {
        value = null;
        query = "";
      }
      open = false;
      activeIndex = -1;
    }, 100);
  }

  function handleKeydown(e: KeyboardEvent) {
    if (!open) {
      if (e.key === "ArrowDown") {
        open = true;
      }
      return;
    }
    if (e.key === "ArrowDown") {
      e.preventDefault();
      activeIndex = Math.min(activeIndex + 1, filtered.length - 1);
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      activeIndex = Math.max(activeIndex - 1, 0);
    } else if (e.key === "Enter") {
      e.preventDefault();
      if (activeIndex >= 0 && filtered[activeIndex])
        select(filtered[activeIndex]);
    } else if (e.key === "Escape") {
      open = false;
      activeIndex = -1;
    }
  }
</script>

<div class="relative floating">
  <input
    {id}
    {placeholder}
    {required}
    class="{className}  {submitted ? 'submitted' : ''}"
    autocomplete="off"
    role="combobox"
    aria-expanded={open}
    aria-controls="{id}-list"
    aria-autocomplete="list"
    bind:value={query}
    oninput={handleInput}
    onfocus={() => (open = true)}
    onblur={handleBlur}
    onkeydown={handleKeydown}
  />
  {#if label}
    <label class="input-label" for={id}>{label}</label>
  {/if}

  {#if open && filtered.length > 0}
    <ul
      id="{id}-list"
      role="listbox"
      class="absolute z-10 w-full mt-1 max-h-64 overflow-y-auto bg-surface border border-surface-alt rounded-lg shadow-sm"
    >
      {#each filtered as item, i}
        <li
          role="option"
          aria-selected={i === activeIndex}
          class="px-3 py-2 text-sm cursor-pointer {i === activeIndex
            ? 'bg-surface-alt'
            : ''}"
          onmousedown={(e) => {
            e.preventDefault();
            select(item);
          }}
          onmouseenter={() => (activeIndex = i)}
        >
          {#if itemSnippet}
            {@render itemSnippet(item)}
          {:else}
            {getLabel(item)}
          {/if}
        </li>
      {/each}
    </ul>
  {/if}
  <p class="input-invalid">{getError()}</p>
</div>
