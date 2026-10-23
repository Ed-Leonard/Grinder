<script lang="ts">
  import { onMount, tick, untrack } from "svelte";
  import ErrorPopup from "$lib/components/utils/Error.svelte";
  import Modal from "$lib/components/utils/Modal.svelte";
  import Xicon from "$lib/components/svgs/xicon.svelte";
  import { tooltip } from "$lib/utils/tooltip";
  import Leftarrow from "$lib/components/svgs/leftarrow.svelte";
  import Rightarrow from "$lib/components/svgs/rightarrow.svelte";

  const THUMBNAIL_TIMEOUT_MS = 10000;

  let {
    file = null,
    onUpload,
    onCancel,
    maxFiles = 5,
    autoOpenPicker = false,
  }: {
    file?: File | null;
    onUpload: (files: File[]) => Promise<void>;
    onCancel: () => void;
    maxFiles?: number;
    autoOpenPicker?: boolean;
  } = $props();

  let previewUrl: string = $state("");
  let thumbnailReady: boolean = $state(false);
  let showVideoPreview: boolean = $state(false);
  let videoPlayer: HTMLVideoElement | null = $state(null);
  let uploading: boolean = $state(false);
  let error: string = $state("");

  let files: File[] = $state(untrack(() => (file ? [file] : [])));
  let index: number = $state(0);
  let current: File | null = $derived(files[index] ?? null);
  let fileInput: HTMLInputElement | null = $state(null);

  let isImage: boolean = $derived(current?.type.startsWith("image/") ?? false);
  let isVideo: boolean = $derived(current?.type.startsWith("video/") ?? false);

  onMount(() => {
    if (autoOpenPicker && fileInput) {
      fileInput.click();
    }
  });

  let thumbnailFailed: boolean = $state(false);
  let thumbnailTimer: ReturnType<typeof setTimeout> | undefined;

  $effect(() => {
    if (!current) {
      previewUrl = "";
      thumbnailReady = false;
      thumbnailFailed = false;
      showVideoPreview = false;
      return;
    }

    const objectUrl: string = URL.createObjectURL(current);
    previewUrl = objectUrl;
    thumbnailReady = false;
    thumbnailFailed = false;
    showVideoPreview = false;

    if (current.type.startsWith("video/")) {
      thumbnailTimer = setTimeout(() => {
        if (!thumbnailReady) {
          thumbnailFailed = true;
        }
      }, THUMBNAIL_TIMEOUT_MS);
    }

    return () => {
      clearThumbnailTimer();
      URL.revokeObjectURL(objectUrl);
    };
  });

  function clearThumbnailTimer(): void {
    if (thumbnailTimer !== undefined) {
      clearTimeout(thumbnailTimer);
      thumbnailTimer = undefined;
    }
  }
  function thumbnailFrameReady(): void {
    clearThumbnailTimer();
    thumbnailReady = true;
    thumbnailFailed = false;
  }

  function thumbnailFrameFailed(): void {
    clearThumbnailTimer();
    thumbnailReady = false;
    thumbnailFailed = true;
  }

  function selectThumbnailFrame(event: Event): void {
    const video: HTMLVideoElement = event.currentTarget as HTMLVideoElement;

    if (!Number.isFinite(video.duration) || video.duration <= 0) {
      thumbnailFrameFailed();
      return;
    }
    try {
      video.currentTime = video.duration / 2;
    } catch {
      thumbnailFrameFailed();
    }
  }

  async function playVideoPreview(): Promise<void> {
    showVideoPreview = true;
    await tick();

    try {
      await videoPlayer?.play();
    } catch {
      // The video controls remain available if autoplay is blocked.
    }
  }

  function prevFile(): void {
    if (files.length === 0) return;
    index = (index - 1 + files.length) % files.length;
  }

  function nextFile(): void {
    if (files.length === 0) return;
    index = (index + 1) % files.length;
  }
  function filesSelected(event: Event): void {
    const input = event.currentTarget as HTMLInputElement;
    const selected = input.files;
    if (!selected || selected.length === 0) return;

    const availableSlots: number = maxFiles - files.length;
    if (availableSlots <= 0) {
      error = `You can upload up to ${maxFiles} attachments at a time.`;
      input.value = "";
      return;
    }

    const selectedFiles: File[] = Array.from(selected);
    files = [...files, ...selectedFiles.slice(0, availableSlots)];
    index = files.length - 1;
    error =
      selectedFiles.length > availableSlots
        ? `Only ${availableSlots} more ${availableSlots === 1 ? "attachment" : "attachments"} can be added.`
        : "";
    input.value = "";
  }

  function removeCurrent(): void {
    if (files.length === 0) return;

    const removeIndex = index;
    files = files.filter((_, i) => i !== removeIndex);

    if (files.length === 0) {
      index = 0;
      error = "";
      return;
    }

    index = Math.min(index, files.length - 1);
    error = "";
  }

  function formatFileSize(bytes: number): string {
    const megabytes: number = bytes / (1024 * 1024);
    return `${megabytes.toFixed(2)} MB`;
  }

  async function upload(): Promise<void> {
    if (files.length === 0) {
      return;
    }

    uploading = true;
    error = "";

    try {
      await onUpload(files);
    } catch (uploadError: unknown) {
      error =
        uploadError instanceof Error
          ? uploadError.message
          : "The attachment could not be uploaded";
    } finally {
      uploading = false;
    }
  }

  function cancel(): void {
    if (!uploading) {
      onCancel();
    }
  }

  function handleEnter(event: KeyboardEvent): void {
    if (event.key !== "Enter") return;
    if (uploading || files.length === 0) return;

    const target = event.target as HTMLElement | null;
    if (target && (target.tagName === "BUTTON" || target.tagName === "A")) {
      return;
    }

    upload();
  }
</script>

<Modal
  onClose={cancel}
  maxWidth="max-w-2xl"
  layer="z-[60]"
  onkeydown={handleEnter}
>
  {#snippet header()}
    <div>
      <h2 class="text-xl font-semibold justify-self-start text-start">
        Confirm {files.length === 1 ? "attachment" : "attachments"}
      </h2>
      <p class="mt-1 text-sm text-text-muted">
        Check the selected {files.length === 1 ? "file" : "files"} before uploading.
      </p>
    </div>
  {/snippet}

  <input
    class="hidden"
    type="file"
    accept="image/png,image/jpeg,image/svg+xml,image/gif,video/mp4,video/quicktime,video/webm,.mov"
    multiple
    bind:this={fileInput}
    onchange={filesSelected}
  />

  <div class="max-h-[70vh] space-y-4 overflow-y-auto p-6">
    {#if error}
      <ErrorPopup {error} />
    {/if}

    <div class="relative group">
      {#if files.length === 0}
        <div
          class="flex min-h-80 items-center justify-center overflow-hidden rounded-lg border border-surface-alt"
        >
          <p class="text-sm text-text-muted">No files selected.</p>
        </div>
      {:else if previewUrl && isImage}
        <div
          class="flex min-h-80 items-center justify-center overflow-hidden rounded-lg border border-surface-alt"
        >
          <img
            class="max-h-112 w-full object-contain"
            src={previewUrl}
            alt="Preview of {current.name}"
          />
        </div>
      {:else if previewUrl && isVideo}
        {#if showVideoPreview}
          <section aria-label="Video preview">
            <video
              bind:this={videoPlayer}
              class="aspect-video w-full rounded-lg bg-black"
              src={previewUrl}
              controls
              playsinline
            >
              <track kind="captions" />
            </video>
          </section>
        {:else}
          <button
            type="button"
            class="relative flex aspect-video w-full items-center justify-center overflow-hidden rounded-lg border border-surface-alt bg-black"
            aria-label="Play video preview"
            onclick={playVideoPreview}
          >
            <video
              class="pointer-events-none h-full w-full object-contain"
              src={previewUrl}
              aria-label="Thumbnail for {current?.name ?? 'selected file'}"
              preload="auto"
              muted
              playsinline
              onloadedmetadata={selectThumbnailFrame}
              onseeked={thumbnailFrameReady}
              onerror={thumbnailFrameFailed}
            >
              <track kind="captions" />
            </video>

            {#if !thumbnailReady && !thumbnailFailed}
              <span
                class="absolute inset-0 flex flex-col items-center justify-center gap-3 bg-black text-sm text-white"
                role="status"
              >
                Generating thumbnail...

                <span
                  class="h-1 w-48 overflow-hidden rounded-full bg-white/30"
                  aria-hidden="true"
                >
                  <span
                    class="thumbnail-progress block h-full w-1/3 rounded-full bg-white"
                  ></span>
                </span>
              </span>
            {:else if thumbnailFailed}
              <span
                class="absolute inset-x-0 bottom-0 bg-black/75 px-4 py-3 text-sm text-white"
              >
                Thumbnail unavailable - click to play the video
              </span>
            {:else}
              <span
                class="absolute inset-x-0 bottom-0 bg-black/60 px-4 py-2 text-sm text-white"
              >
                Click the video to preview
              </span>
            {/if}
          </button>
        {/if}
      {:else}
        <div
          class="flex min-h-80 items-center justify-center overflow-hidden rounded-lg border border-surface-alt"
        >
          <p class="text-sm text-text-muted">
            A preview is not available for this file.
          </p>
        </div>
      {/if}

      {#if files.length > 1}
        <button
          type="button"
          class="absolute left-2 top-1/2 -translate-y-1/2 icon-button bg-surface-alt hover:bg-surface-alt-hover group-hover:opacity-100 pointer-fine:opacity-0 transition-[opacity, colors] opacity-80"
          onclick={prevFile}
          disabled={uploading}
          aria-label="Previous attachment"
          use:tooltip={"Previous attachment"}
        >
          <Leftarrow />
        </button>
        <button
          type="button"
          class="absolute right-2 top-1/2 -translate-y-1/2 icon-button bg-surface-alt hover:bg-surface-alt-hover group-hover:opacity-100 pointer-fine:opacity-0 transition-[opacity, colors] opacity-80"
          onclick={nextFile}
          disabled={uploading}
          aria-label="Next attachment"
          use:tooltip={"Next attachment"}
        >
          <Rightarrow />
        </button>
        <div
          class="absolute bottom-3 left-1/2 -translate-x-1/2 rounded-full bg-black/65 px-3 py-1 text-xs text-white"
        >
          {index + 1} / {files.length}
        </div>
      {/if}
    </div>

    <div class="flex justify-between gap-4 rounded-lg px-4 py-2 bg-surface-alt">
      {#if current}
        <div class="min-w-0">
          <p class="truncate font-medium">{current.name}</p>
          <p class="mt-1 text-sm text-text-muted">
            {current.type || "Unknown file type"}
          </p>
        </div>

        <div class="flex shrink-0 items-center gap-2">
          <p class="text-sm text-text-muted">{formatFileSize(current.size)}</p>
          <button
            type="button"
            class="icon-button hover:text-red-500"
            onclick={removeCurrent}
            disabled={uploading}
            aria-label="Remove attachment"
            use:tooltip={"Remove attachment"}
          >
            <Xicon size={1} />
          </button>
        </div>
      {:else}
        <p class="text-sm text-text-muted">No files selected.</p>
      {/if}
    </div>

    <button
      type="button"
      class="secondary-button w-full"
      onclick={() => fileInput?.click()}
      disabled={uploading || files.length >= maxFiles}
    >
      {files.length >= maxFiles ? "Attachment limit reached" : "Add more"}
    </button>
  </div>

  {#snippet footer()}
    <div class="ml-auto flex items-center gap-3">
      <button
        type="button"
        class="secondary-button"
        onclick={cancel}
        disabled={uploading}
      >
        Cancel
      </button>

      <button
        type="button"
        class="primary-button"
        onclick={upload}
        disabled={uploading || files.length === 0}
      >
        {uploading
          ? "Uploading..."
          : files.length <= 1
            ? "Upload"
            : `Upload ${files.length} files`}
      </button>
    </div>
  {/snippet}
</Modal>

<style>
  .thumbnail-progress {
    animation: thumbnail-loading 1.2s ease-in-out infinite;
  }
  @keyframes thumbnail-loading {
    from {
      transform: translateX(-100%);
    }

    to {
      transform: translateX(300%);
    }
  }

  @media (prefers-reduced-motion: reduce) {
    .thumbnail-progress {
      animation: none;
      width: 100%;
      opacity: 0.6;
    }
  }
</style>
