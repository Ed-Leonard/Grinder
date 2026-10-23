<script lang="ts">
  /**
   * CroppingTool is a custom image cropper with a circular viewport and uses pan and zoom features.
   *
   * Features:
   * - Circular viewport for cropping images
   * - The user can drag to pan the image within the viewport.
   * - The user can scroll to zoom in and out of the image.
   * - For every pan or zoome action the cropped region of the image is rendered to a canvas
   *   and exported as a PNG file.
   *
   * Props:
   * - sourceImageUrl: The URL of the image to be cropped.
   * - outputSize: The size of the output cropped image in pixels.
   * - onCropChange: Callback function fired on every crop update.
   */
  import { onMount } from "svelte";

  type CropPayload = {
    file: File;
    previewUrl: string;
  };

  // Inputs provided by parent
  let {
    sourceImageUrl,
    outputSize = 512,
    onCropChange,
  }: {
    sourceImageUrl: string | undefined;
    outputSize?: number;
    onCropChange?: (payload: CropPayload | null) => void;
  } = $props();

  // DOM element references used for measuring viewport and drawing image
  let viewportElement = $state<HTMLDivElement | undefined>(undefined);
  let sourceImageElement = $state<HTMLImageElement | undefined>(undefined);
  let viewportSize = $state(260);

  // Dimensions of the source image
  let naturalWidth = $state(0);
  let naturalHeight = $state(0);

  let zoomLevel = $state(1);
  let minZoom = $state(1);
  let maxZoom = 4;

  // Keeps track of translation from viewport centre
  let offsetX = $state(0);
  let offsetY = $state(0);

  let isDragging = $state(false);
  let dragStartX = 0;
  let dragStartY = 0;

  // Preview generated from the current crop
  let exportedPreviewUrl: string | null = $state(null);
  let hasLoadedImage = $state(false);

  function measureViewport() {
    if (!viewportElement) {
      return;
    }

    viewportSize = viewportElement.clientWidth;
    clampOffsets();
    if (hasLoadedImage) {
      updateCrop();
    }
  }

  function getScaledWidth(zoom = zoomLevel) {
    return naturalWidth * zoom;
  }

  function getScaledHeight(zoom = zoomLevel) {
    return naturalHeight * zoom;
  }

  /**
   * Prevents the image from being dragged outside of the viewport
   */
  function clampOffsets() {
    if (!hasLoadedImage) {
      offsetX = 0;
      offsetY = 0;
      return;
    }

    const overflowX = Math.max(0, (getScaledWidth() - viewportSize) / 2);
    const overflowY = Math.max(0, (getScaledHeight() - viewportSize) / 2);

    offsetX = Math.min(overflowX, Math.max(-overflowX, offsetX));
    offsetY = Math.min(overflowY, Math.max(-overflowY, offsetY));
  }

  /**
   * Initializes the cropper once image has loaded.
   * Calculates minimum zoom for the image.
   */
  function initializeForImage() {
    if (!sourceImageElement || !viewportElement) {
      return;
    }

    naturalWidth = sourceImageElement.naturalWidth;
    naturalHeight = sourceImageElement.naturalHeight;
    hasLoadedImage = naturalWidth > 0 && naturalHeight > 0;

    if (!hasLoadedImage) {
      onCropChange?.(null);
      return;
    }

    // Scales the image so that the shortest dimension is covered by the viewport
    const baseZoom = Math.max(
      viewportSize / naturalWidth,
      viewportSize / naturalHeight,
    );
    minZoom = baseZoom;
    zoomLevel = baseZoom;
    offsetX = 0;
    offsetY = 0;
    updateCrop();
  }

  function onImageLoad() {
    initializeForImage();
  }

  function onPointerDown(event: PointerEvent) {
    if (!hasLoadedImage || !viewportElement) {
      return;
    }

    isDragging = true;
    dragStartX = event.clientX - offsetX;
    dragStartY = event.clientY - offsetY;
    viewportElement.setPointerCapture(event.pointerId);
  }

  function onPointerMove(event: PointerEvent) {
    if (!isDragging) {
      return;
    }

    offsetX = event.clientX - dragStartX;
    offsetY = event.clientY - dragStartY;
    clampOffsets();
    updateCrop();
  }

  function stopDragging(event: PointerEvent) {
    if (!isDragging || !viewportElement) {
      return;
    }

    isDragging = false;
    if (viewportElement.hasPointerCapture(event.pointerId)) {
      viewportElement.releasePointerCapture(event.pointerId);
    }
  }

  function onWheel(event: WheelEvent) {
    if (!hasLoadedImage || !viewportElement) {
      return;
    }

    event.preventDefault();

    const previousZoom = zoomLevel;
    const zoomStep = event.deltaY < 0 ? 1.08 : 1 / 1.08;
    const nextZoom = Math.min(
      maxZoom,
      Math.max(minZoom, previousZoom * zoomStep),
    );

    if (nextZoom === previousZoom) {
      return;
    }

    const rect = viewportElement.getBoundingClientRect();
    const pointerX = event.clientX - rect.left - rect.width / 2;
    const pointerY = event.clientY - rect.top - rect.height / 2;

    offsetX = pointerX - (nextZoom / previousZoom) * (pointerX - offsetX);
    offsetY = pointerY - (nextZoom / previousZoom) * (pointerY - offsetY);
    zoomLevel = nextZoom;

    clampOffsets();
    updateCrop();
  }

  // Converts the content of canvas to a PNG blob
  function toBlob(canvas: HTMLCanvasElement): Promise<Blob> {
    return new Promise((resolve, reject) => {
      canvas.toBlob((blob) => {
        if (!blob) {
          reject(new Error("Failed to create cropped image"));
          return;
        }
        resolve(blob);
      }, "image/png");
    });
  }

  /**
   * Generates a sqaure cropped image based on current zoom and pan levels.
   * The crop is rendered to a canvas, then exported as a PNG
   */
  async function updateCrop() {
    if (!hasLoadedImage || !sourceImageElement) {
      onCropChange?.(null);
      return;
    }

    const cropCanvas = document.createElement("canvas");
    cropCanvas.width = outputSize;
    cropCanvas.height = outputSize;

    const context = cropCanvas.getContext("2d");
    if (!context) {
      onCropChange?.(null);
      return;
    }

    // Convert viewport measurements to source image coordinates
    const sourceX =
      naturalWidth / 2 - viewportSize / (2 * zoomLevel) - offsetX / zoomLevel;
    const sourceY =
      naturalHeight / 2 - viewportSize / (2 * zoomLevel) - offsetY / zoomLevel;
    const sourceSize = viewportSize / zoomLevel;

    context.clearRect(0, 0, outputSize, outputSize);
    context.drawImage(
      sourceImageElement,
      sourceX,
      sourceY,
      sourceSize,
      sourceSize,
      0,
      0,
      outputSize,
      outputSize,
    );

    try {
      const blob = await toBlob(cropCanvas);
      const croppedFile = new File([blob], "profile-picture.png", {
        type: "image/png",
      });

      if (exportedPreviewUrl) {
        URL.revokeObjectURL(exportedPreviewUrl);
      }

      exportedPreviewUrl = URL.createObjectURL(croppedFile);
      onCropChange?.({
        file: croppedFile,
        previewUrl: exportedPreviewUrl,
      });
    } catch {
      onCropChange?.(null);
    }
  }

  /**
   * Monitor viewport size changes and remeasure the viewport when it does change.
   * Crop calculatins remain correct when component is resized
   */
  onMount(() => {
    const resizeObserver = new ResizeObserver(() => {
      measureViewport();
    });

    if (viewportElement) {
      resizeObserver.observe(viewportElement);
      measureViewport();
    }

    return () => {
      resizeObserver.disconnect();
      if (exportedPreviewUrl) {
        URL.revokeObjectURL(exportedPreviewUrl);
      }
    };
  });

  // Reset crop state when loading a new image
  $effect(() => {
    sourceImageUrl;
    hasLoadedImage = false;
    naturalWidth = 0;
    naturalHeight = 0;
    zoomLevel = 1;
    minZoom = 1;
    offsetX = 0;
    offsetY = 0;
    onCropChange?.(null);
  });

  const imageStyle = $derived(
    `left: 50%; top: 50%; width: ${naturalWidth}px; height: ${naturalHeight}px; transform: translate(-50%, -50%) translate(${offsetX}px, ${offsetY}px) scale(${zoomLevel});`,
  );
</script>

<div class="w-full flex justify-center">
  <div
    class="relative w-[min(70vw,260px)] aspect-square overflow-hidden border-2 border-surface-alt rounded-full bg-surface touch-none cursor-all-scroll"
    bind:this={viewportElement}
    onpointerdown={onPointerDown}
    onpointermove={onPointerMove}
    onpointerup={stopDragging}
    onpointerleave={stopDragging}
    onwheel={onWheel}
    role="presentation"
  >
    {#if sourceImageUrl}
      <img
        bind:this={sourceImageElement}
        src={sourceImageUrl}
        alt="Crop preview"
        class="absolute max-w-none select-none pointer-events-none"
        style={imageStyle}
        draggable="false"
        onload={onImageLoad}
      />
    {/if}

    <svg
      class="absolute inset-0 w-full h-full pointer-events-none"
      xmlns="http://www.w3.org/2000/svg"
    >
      <line
        x1="33.33%"
        y1="0"
        x2="33.33%"
        y2="100%"
        stroke="black"
        stroke-width="1"
        opacity="0.7"
      />
      <line
        x1="66.66%"
        y1="0"
        x2="66.66%"
        y2="100%"
        stroke="black"
        stroke-width="1"
        opacity="0.7"
      />

      <line
        x1="0"
        y1="33.33%"
        x2="100%"
        y2="33.33%"
        stroke="black"
        stroke-width="1"
        opacity="0.7"
      />
      <line
        x1="0"
        y1="66.66%"
        x2="100%"
        y2="66.66%"
        stroke="black"
        stroke-width="1"
        opacity="0.7"
      />
    </svg>
  </div>
</div>
