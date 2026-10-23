<script lang="ts">
  import { onDestroy } from "svelte";
  import CroppingTool from "./CroppingTool.svelte";
  import Xicon from "../svgs/xicon.svelte";

  let {
    previewUrl,
    onCancel,
    onConfirm,
    selectedFile: incomingSelectedFile,
  } = $props();

  const MAX_IMAGE_FILE_SIZE = 5 * 1024 * 1024;
  const ALLOWED_TYPES = [
    "image/jpeg",
    "image/png",
    "image/svg+xml",
    "image/gif",
  ];

  let selectedImageUrl = $state<string | undefined>(undefined);
  let selectedFile: File | null = $state(null);
  let croppedFile: File | null = $state(null);
  let isUploading = $state(false);
  let alt = "Profile Picture";
  let errorMessage = $state("");

  onDestroy(() => {});

  $effect(() => {
    if (incomingSelectedFile) {
      selectedFile = incomingSelectedFile as File;
      croppedFile = null;
    }
    selectedImageUrl = previewUrl;
    errorMessage = "";
  });

  function onCropChanged(payload: { file: File } | null) {
    if (!payload) {
      croppedFile = null;
      return;
    }
    croppedFile = payload.file;
  }

  function handleConfirm() {
    const fileToUpload = croppedFile ?? selectedFile;

    if (!fileToUpload) {
      errorMessage = "Please select or crop an image.";
      return;
    }

    if (selectedFile && !ALLOWED_TYPES.includes(selectedFile.type)) {
      errorMessage =
        "Invalid image, supported file types are .jpeg, .png, .svg, .gif";
      return;
    }

    if (selectedFile && selectedFile.size > MAX_IMAGE_FILE_SIZE) {
      errorMessage = "Image too large, maximum file size is 5MB";
      return;
    }

    onConfirm?.({ file: fileToUpload });
  }

  function cancelEdit() {
    if (onCancel) {
      onCancel();
    }
  }
</script>

{#if true}
  <div
    class="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm"
  >
    <div class=" bg-surface rounded-lg shadow-xl flex flex-col w-md">
      <div
        class="flex items-center justify-between p-4 border-b border-surface-alt"
      >
        <h2>Edit Profile Picture</h2>
        <button
          type="button"
          class="icon-button"
          onclick={cancelEdit}
          aria-label="Close modal"
        >
          <Xicon />
        </button>
      </div>

      <div class="p-6 text-center">
        <div
          class="w-full bg-surface-alt/20 flex flex-col items-center justify-center rounded p-4 min-h-72"
        >
          {#if selectedImageUrl != undefined}
            <div class="flex flex-col items-center w-full">
              <p class="text-sm font-medium text-text-muted mb-2">
                Crop Your Profile Picture
              </p>
              <CroppingTool
                sourceImageUrl={selectedImageUrl}
                onCropChange={onCropChanged}
              />
            </div>
          {:else}
            <div
              class="w-full h-full flex items-center justify-center text-text-muted"
              aria-label={alt}
            >
              <svg
                xmlns="http://www.w3.org/2000/svg"
                width="48"
                height="48"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
                stroke-linecap="round"
                stroke-linejoin="round"
              >
                <rect x="3" y="3" width="18" height="18" rx="2" ry="2" />
                <circle cx="8.5" cy="8.5" r="1.5" />
                <polyline points="21 15 16 10 5 21" />
              </svg>
            </div>
          {/if}
        </div>
        {#if errorMessage}
          <div
            class="flex items-start gap-2 p-2 text-sm text-red-500 rounded mb-4"
          >
            <span>{errorMessage}</span>
          </div>
        {/if}
      </div>
      <div
        class="flex items-center gap-3 px-6 py-4 border-t border-surface-alt"
      >
        <button type="button" class="secondary-button" onclick={cancelEdit}>
          Cancel
        </button>
        <button
          type="button"
          class="primary-button flex-1 flex items-center justify-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed"
          disabled={isUploading}
          onclick={handleConfirm}
        >
          {#if isUploading}
            <svg
              class="animate-spin h-5 w-5"
              xmlns="http://www.w3.org/2000/svg"
              fill="none"
              viewBox="0 0 24 24"
            >
              <circle
                class="opacity-25"
                cx="12"
                cy="12"
                r="10"
                stroke="currentColor"
                stroke-width="4"
              ></circle>
              <path
                class="opacity-75"
                fill="currentColor"
                d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
              ></path>
            </svg>
            Uploading...
          {:else}
            <svg
              xmlns="http://www.w3.org/2000/svg"
              width="20"
              height="20"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              stroke-width="2"
              stroke-linecap="round"
              stroke-linejoin="round"
            >
              <polyline points="20 6 9 17 4 12" />
            </svg>
            Confirm
          {/if}
        </button>
      </div>
    </div>
  </div>
{/if}
