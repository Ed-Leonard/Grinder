<script lang="ts">
  import { onDestroy } from "svelte";
  import EditProfilePicture from "./EditProfilePicture.svelte";
  import Head from "../svgs/head.svelte";
  import Pencil from "../svgs/pencil.svelte";
  import { tooltip } from "$lib/utils/tooltip";

  let {
    profilePicture,
    onRefresh,
    editable = false,
    overridePreviewUrl = undefined,
    onConfirm,
  } = $props();

  let showEditModal = $state(false);
  let alt = "Profile Picture";
  let imageLoadFailed = $state(false);
  let fileInput: HTMLInputElement;
  let transientSelectedObjectUrl: string | null = null;
  let selectedFile: File | null = $state(null);
  let selectedImageUrl = $state<string | undefined>(undefined);

  let effectiveSrc = $derived(overridePreviewUrl ?? profilePicture);

  $effect(() => {
    profilePicture;
    selectedFile = null;
    selectedImageUrl = undefined;
  });

  $effect(() => {
    effectiveSrc;
    imageLoadFailed = false;
  });

  function revokeTransientObjectUrl() {
    if (transientSelectedObjectUrl) {
      URL.revokeObjectURL(transientSelectedObjectUrl);
      transientSelectedObjectUrl = null;
    }
  }

  onDestroy(() => {
    revokeTransientObjectUrl();
  });

  function openFilePicker() {
    if (!editable) return;
    fileInput.value = "";
    fileInput.click();
  }

  function onFileSelected(e: Event) {
    const target = e.target as HTMLInputElement;
    const files = target.files;

    if (files && files.length > 0) {
      const file = files[0];

      revokeTransientObjectUrl();
      transientSelectedObjectUrl = URL.createObjectURL(file);
      selectedFile = file;
      selectedImageUrl = transientSelectedObjectUrl;

      openEditModal();
    }
  }

  function openEditModal() {
    showEditModal = true;
  }

  function closeEditModal() {
    showEditModal = false;
    onRefresh();
  }
</script>

<div>
  <div class="group relative w-28 h-28">
    {#if effectiveSrc && !imageLoadFailed}
      <img
        src={overridePreviewUrl ?? profilePicture}
        {alt}
        class="w-full h-full rounded-full border-4 border-surface-alt object-cover"
        onerror={() => {
          imageLoadFailed = true;
        }}
      />
    {:else}
      <div
        class="w-full h-full rounded-full border-4 border-surface-alt-hover bg-surface-alt flex items-center justify-center text-text-muted"
        aria-label={alt}
      >
        <Head />
      </div>
    {/if}
    {#if editable}
      <button
        type="button"
        class="absolute right-0 bottom-0 bg-brand hover:bg-brand-alt text-surface shadow-sm icon-button"
        aria-label="Edit profile picture"
        use:tooltip={"Edit profile picture"}
        onclick={openFilePicker}
      >
        <Pencil />
      </button>
    {/if}
  </div>
</div>

{#if showEditModal}
  <EditProfilePicture
    previewUrl={selectedImageUrl ?? profilePicture}
    {selectedFile}
    onCancel={closeEditModal}
    onConfirm={(detail: { file: File }) => {
      onConfirm?.(detail);
      closeEditModal();
    }}
  />
{/if}

<input
  bind:this={fileInput}
  type="file"
  accept="image/jpeg,image/png,image/gif,image/svg+xml"
  class="hidden"
  onchange={onFileSelected}
/>
