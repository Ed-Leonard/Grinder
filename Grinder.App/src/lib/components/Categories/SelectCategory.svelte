<script lang="ts">
  import { onMount } from "svelte";
  import { fetchCategories } from "$lib/api/category";
  import type { Category } from "$lib/api/category";
  import Pluscircle from "../svgs/pluscircle.svelte";

  let {
    onSelect,
  }: {
    onSelect: (category: Category) => void;
  } = $props();

  let isEditing = $state(false);
  let isFocused = $state(false);

  let query = $state("");
  let inputElement: HTMLInputElement | undefined = $state();

  let allCategories: Category[] = $state([]);

  let highlightedIndex = $state(-1);

  let filteredCategories = $derived(
    query.trim()
      ? allCategories.filter((c) =>
          c.name.toLowerCase().includes(query.trim().toLowerCase()),
        )
      : allCategories,
  );

  // Temp ids for categories that don't exist yet, so CategoryList's
  // `{#each categories as category (category.id)}` still has a unique key.
  // These get replaced with real ids when the category is actually
  // created (see CategoryList.svelte / submit-time creation).
  let tempIdCounter = -1;

  onMount(async () => {
    try {
      allCategories = await fetchCategories();
    } catch (error) {
      console.error("Error fetching categories:", error);
    }
  });

  function startEditing() {
    isEditing = true;
    isFocused = true;
    query = "";
    highlightedIndex = -1;
    setTimeout(() => inputElement?.focus(), 0);
  }

  function closeEditing() {
    isFocused = false;
    if (query != "") return;
    isEditing = false;
    highlightedIndex = -1;
  }

  function selectCategory(category: Category) {
    onSelect(category);
    isEditing = false;
    closeEditing();
  }

  // Adds a typed name as a pending (not-yet-created) category.
  function selectNewCategory(name: string) {
    const trimmed = name.trim();
    if (!trimmed) return;

    const existing = allCategories.find(
      (c) => c.name.toLowerCase() === trimmed.toLowerCase(),
    );

    if (existing) {
      selectCategory(existing);
      return;
    }

    selectCategory({ id: tempIdCounter--, name: trimmed } as Category);
  }

  function handleFocus() {
    isFocused = true;
  }

  function handleBlur() {
    closeEditing();
  }

  function addCategory(e: Event) {
    e.preventDefault();
    if (highlightedIndex >= 0 && highlightedIndex < filteredCategories.length) {
      selectCategory(filteredCategories[highlightedIndex]);
    } else {
      selectNewCategory(query);
    }
  }

  function handleKeydown(e: KeyboardEvent) {
    if (e.key === "Enter") {
      addCategory(e);
    } else if (e.key === "Escape") {
      closeEditing();
    } else if (e.key === "ArrowDown") {
      e.preventDefault();
      highlightedIndex = Math.min(
        highlightedIndex + 1,
        filteredCategories.length - 1,
      );
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      highlightedIndex = Math.max(highlightedIndex - 1, -1);
    }
  }
</script>

{#if isEditing}
  <div class="relative">
    <div class="inline-flex gap-2 items-center">
      <div class="relative">
        <input
          bind:this={inputElement}
          bind:value={query}
          onkeydown={handleKeydown}
          onfocus={handleFocus}
          onblur={handleBlur}
          type="text"
          placeholder="Category name..."
          class="search-field focus:outline-brand"
          role="combobox"
          aria-expanded={isFocused && filteredCategories.length > 0}
          aria-controls="category-listbox"
          aria-autocomplete="list"
        />

        {#if isFocused && filteredCategories.length > 0}
          <ul
            id="category-listbox"
            role="listbox"
            class="w-full absolute z-10 top-full left-0 mt-1 max-h-64 overflow-auto rounded-lg border border-surface-alt bg-surface shadow-sm"
            onmousedown={(e) => e.preventDefault()}
          >
            {#each filteredCategories as category, i}
              <li role="option" aria-selected={highlightedIndex === i}>
                <button
                  type="button"
                  class="w-full truncate text-left px-4 py-2 text-sm hover:bg-surface-alt {highlightedIndex ===
                  i
                    ? 'bg-surface-alt'
                    : ''}"
                  onclick={() => selectCategory(category)}
                >
                  {category.name}
                </button>
              </li>
            {/each}
          </ul>
        {/if}
      </div>
      <button
        class="secondary-button inline-flex items-center gap-2"
        type="button"
        onclick={addCategory}><Pluscircle />Add</button
      >
    </div>
  </div>
{:else}
  <button
    class="secondary-button flex items-center gap-2"
    onclick={startEditing}
  >
    <Pluscircle />
    Add Category
  </button>
{/if}
