<script lang="ts">
  import SelectCategory from "./SelectCategory.svelte";
  import type { Category } from "$lib/api/category";
  import Xicon from "../svgs/xicon.svelte";

  // Categories already attached to the task/parent. Bind this from
  // the parent (e.g. TaskForm.svelte) so it can read the final list
  // on submit: <CategoryList bind:categories={task.categories} />
  export let categories: Category[] = [];

  function handleSelect(category: Category) {
    // Avoid adding the same category twice
    if (
      categories.some((c) => c.id === category.id || c.name === category.name)
    )
      return;
    categories = [...categories, category];
  }

  function removeCategory(id: number) {
    categories = categories.filter((c) => c.id !== id);
  }
</script>

<div class="flex flex-wrap items-center gap-2 w-full max-h-64">
  <div>
    <SelectCategory onSelect={handleSelect} />
  </div>

  {#each categories as category (category.id)}
    <button
      class="flex items-center gap-2 secondary-button hover:text-red-500"
      type="button"
      onclick={() => removeCategory(category.id)}
      aria-label={`Remove category ${category.name}`}
    >
      <span>{category.name}</span>
      <Xicon />
    </button>
  {/each}
</div>
