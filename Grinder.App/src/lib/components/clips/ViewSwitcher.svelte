<script lang="ts">
  import type { Component } from "svelte";

  type ViewOption = {
    value: string;
    label: string;
    icon?: Component<{ size?: number }>;
  };

  let {
    active,
    options,
    onSelect,
  }: { active: string; options: ViewOption[]; onSelect: (value: string) => void } = $props();
</script>

<div class="flex flex-row bg-surface-alt justify-center gap-4 p-4" role="tablist" aria-label="View switcher">
  {#each options as option (option.value)}
    {@const Icon = option.icon}
    <button
      type="button"
      role="tab"
      aria-selected={active === option.value}
      class="w-full sm:w-auto justify-center inline-flex items-center secondary-button gap-2 {active === option.value 
        ? 'bg-brand text-surface' 
        : ''}"
      onclick={() => onSelect(option.value)}
    >
      <Icon size={1.25} />
      <span class="hidden xs:inline">{option.label}</span>
    </button>
  {/each}
</div>