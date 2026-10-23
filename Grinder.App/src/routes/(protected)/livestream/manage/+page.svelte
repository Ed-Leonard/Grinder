<script lang="ts">
  import { onMount } from "svelte";
  import { goto, beforeNavigate } from "$app/navigation";
  import { attachTrack } from "$lib/utils/attach-track";
  import { broadcast } from "$lib/stores/broadcast.svelte";
  import { resolve } from "$app/paths";
  import ChatPanel from "$lib/components/streams/ChatPanel.svelte";
  import CommentIcon from "$lib/components/svgs/commenticon.svelte";
  import { tooltip } from "$lib/utils/tooltip";
  import Viewer from "$lib/components/svgs/livestream/viewer.svelte";
  import Clock from "$lib/components/svgs/clock.svelte";
  import Mic from "$lib/components/svgs/livestream/mic.svelte";
  import MicMute from "$lib/components/svgs/livestream/micMute.svelte";
  import Camera from "$lib/components/svgs/livestream/camera.svelte";
  import CameraOff from "$lib/components/svgs/livestream/cameraOff.svelte";
  import Modal from "$lib/components/utils/Modal.svelte";
  import Select from "$lib/components/FormFields/Select.svelte";

  let showChat = $state(false);
  let streamTimer = $state(0);
  let pendingEndStream = $state(false);
  let timerId: ReturnType<typeof setInterval> | undefined;

  function startStreamTimer() {
    timerId = setInterval(() => {
      streamTimer += 1;
    }, 1000);
  }

  function formatTimer(totalSeconds: number) {
    const h = Math.floor(totalSeconds / 3600);
    const m = Math.floor((totalSeconds % 3600) / 60);
    const s = totalSeconds % 60;
    const pad = (n: number) => n.toString().padStart(2, "0");
    return h > 0 ? `${pad(h)}:${pad(m)}:${pad(s)}` : `${pad(m)}:${pad(s)}`;
  }

  let formattedTimer = $derived(formatTimer(streamTimer));

  function stopStreamTimer() {
    clearInterval(timerId);
    timerId = undefined;
  }

  function handlePageHide() {
    if (broadcast.connected) broadcast.disposeAll();
  }

  function handleBeforeUnload(event: BeforeUnloadEvent) {
    if (!broadcast.connected) return;
    event.preventDefault();
    event.returnValue = "Leaving will end the livestream.";
  }

  beforeNavigate(({ cancel }) => {
    if (
      broadcast.connected &&
      !confirm("Leaving this page will also end the livestream.")
    ) {
      cancel();
    }
  });

  async function endLive() {
    stopStreamTimer();
    await broadcast.endLive();
    await goto(resolve("/livestream/start" as any));
  }

  onMount(() => {
    // Guard against landing here directly without an active stream —
    // e.g. a page reload, which clears the in-memory room/track state.
    if (!broadcast.connected || !broadcast.room) {
      goto(resolve("/livestream/start" as any));
      return;
    }

    startStreamTimer();
    window.addEventListener("beforeunload", handleBeforeUnload);
    window.addEventListener("pagehide", handlePageHide);

    return () => {
      stopStreamTimer();
      window.removeEventListener("beforeunload", handleBeforeUnload);
      window.removeEventListener("pagehide", handlePageHide);
      // If we're leaving without having ended the stream via the button
      // above (e.g. the user confirmed a navigation away anyway), tear
      // the room and tracks down properly rather than leaking them.
      if (broadcast.connected) {
        broadcast.disposeAll();
      }
    };
  });
</script>

<div
  class="relative h-full flex flex-col lg:flex-row lg:items-center lg:justify-center gap-0 lg:gap-3"
>
  <div
    class="w-full h-full min-h-0 bg-surface md:bg-surface-alt flex
           flex-col items-center lg:flex-row lg:items-start lg:justify-center
           gap-0 lg:gap-3 p-0 sm:p-4 lg:p-6 overflow-y-auto overflow-x-hidden"
  >
    {#if broadcast.error}
      <p
        class="w-full max-w-sm rounded-lg border border-red-300 bg-red-50 p-3 text-sm text-red-700"
      >
        {broadcast.error}
      </p>
    {/if}
    <article
      class="relative flex h-full w-full max-w-sm flex-col gap-2 min-h-0"
    >
      <div
        class="relative w-full flex-1 min-h-0 overflow-hidden rounded-lg bg-black shadow-sm"
      >
        <video
          use:attachTrack={broadcast.previewTrack}
          autoplay
          muted
          playsinline
          class="absolute inset-0 h-full w-full object-contain scale-x-[-1]"
        ></video>
        <div
          class="absolute top-3 left-3 z-40 flex items-center gap-2 px-3 py-1.5 text-sm text-white"
        >
          <span class="flex items-center gap-1">
            <Viewer size={1.25} />
            <span class="text-red-500">{broadcast.viewerCount}</span>
          </span>
          <span>·</span>
          <span class="flex items-center gap-1">
            <Clock size={1.25} colour="CurrentColor" />
            <span>{formattedTimer}</span>
          </span>
        </div>

        <div
          class="absolute right-3 bottom-3 z-40 flex flex-col items-center gap-2 sm:right-4 sm:bottom-4"
        >
          <div
            class="flex w-10 flex-col-reverse items-center justify-end gap-1 py-1 sm:w-12"
            aria-hidden="true"
          >
            {#each Array(16) as _, i}
              {@const threshold = i / 16}
              <div
                class="h-1 rounded-sm transition-colors sm:h-1.5 {broadcast.audioLevel >
                threshold
                  ? i > 8
                    ? i > 10
                      ? 'bg-red-500'
                      : 'bg-yellow-500'
                    : 'bg-green-500'
                  : 'bg-white/25'}"
                style="width: {20 + i * 4}%"
              ></div>
            {/each}
          </div>
          <button
            type="button"
            aria-label={broadcast.micMuted
              ? "Unmute microphone"
              : "Mute microphone"}
            use:tooltip={broadcast.micMuted
              ? "Unmute microphone"
              : "Mute microphone"}
            class="icon-button flex items-center justify-center {broadcast.micMuted
              ? ' bg-brand text-red-500 hover:bg-brand'
              : 'bg-[#242426]/30 text-white hover:bg-[#242426]'}"
            disabled={!broadcast.previewAudio}
            onclick={() => broadcast.toggleMic()}
          >
            {#if broadcast.micMuted}
              <MicMute size={1.5} />
            {:else}
              <Mic size={1.5} />
            {/if}
          </button>

          <button
            type="button"
            aria-label={broadcast.cameraMuted
              ? "Turn camera on"
              : "Turn camera off"}
            use:tooltip={broadcast.cameraMuted
              ? "Turn camera on"
              : "Turn camera off"}
            class="icon-button flex items-center justify-center {broadcast.cameraMuted
              ? ' bg-brand text-red-500 hover:bg-brand'
              : 'bg-[#242426]/30 text-white hover:bg-[#242426]'}"
            disabled={!broadcast.previewTrack}
            onclick={() => broadcast.toggleCamera()}
          >
            {#if broadcast.cameraMuted}
              <CameraOff size={1.5} />
            {:else}
              <Camera size={1.5} />
            {/if}
          </button>

          <button
            type="button"
            aria-label="Open chat"
            use:tooltip={showChat ? "Hide chat" : "Show chat"}
            class="icon-button flex items-center justify-center {showChat
              ? 'bg-brand text-surface hover:bg-brand'
              : 'bg-[#242426]/30 hover:bg-[#242426] text-white'}"
            onclick={() => (showChat = !showChat)}
          >
            <CommentIcon size={1.5} />
          </button>
        </div>
        <div
          class="pointer-events-none absolute inset-x-0 bottom-0 p-3 flex flex-col gap-2 text-white"
        >
          <div
            class="pointer-events-auto bg-[#242426]/30 rounded-lg gap-y-1 flex flex-col mr-16 px-4 py-2"
          >
            <span class="wrap-anywhere font-serif font-semibold">
              {broadcast.title}
            </span>
            <span class="wrap-anywhere text-sm text-white/90">
              {broadcast.taskTitle}
            </span>
          </div>
        </div>

        {#if broadcast.remoteTracks.length > 0}
          {#each broadcast.remoteTracks as remote (remote.id)}
            {#if remote.track.kind !== "video"}
              <audio use:attachTrack={remote.track} autoplay></audio>
            {/if}
          {/each}
        {/if}
      </div>

      <div
        class="rounded-lg bg-surface p-4 space-y-4 {showChat
          ? 'hidden lg:block'
          : ''}"
      >
        <div class="flex flex-col md:flex-row gap-2">
          <Select
            id="camerapicker"
            bind:value={broadcast.selectedCameraId}
            items={broadcast.cameras}
            getValue={(device) => device.deviceId}
            getLabel={(device) => device.label}
            onchange={() => broadcast.startPreview()}
            label="Camera"
            emptyLabel="No camera selected"
            submitted={false}
            class="flex-1"
          />
          <Select
            id="audiopicker"
            bind:value={broadcast.selectedMicrophoneId}
            items={broadcast.microphones}
            getValue={(device) => device.deviceId}
            getLabel={(device) => device.label}
            onchange={() => broadcast.enableAudioPreview()}
            label="Audio"
            emptyLabel="No audio selected"
            submitted={false}
            class="flex-1"
          />
        </div>
        <button
          class="destructive-button w-full"
          onclick={() => (pendingEndStream = true)}
        >
          End Stream
        </button>
      </div>
    </article>
    {#if broadcast.roomName}
      <ChatPanel
        roomName={broadcast.roomName}
        isOpen={showChat}
        onClose={() => (showChat = false)}
      />
    {/if}

    {#if pendingEndStream}
      <Modal
        onClose={() => (pendingEndStream = false)}
        maxWidth="max-w-sm"
        layer="z-50"
      >
        {#snippet header()}
          <div>
            <h2 class="font-semibold justify-self-start">End Stream?</h2>
          </div>
        {/snippet}

        <div class="px-6 py-4 text-text-muted">
          <p>Are you sure you want to end your stream?</p>
          <p>This action cannot be undone.</p>
        </div>

        {#snippet footer()}
          <div class="ml-auto flex items-center gap-3">
            <button
              type="button"
              class="secondary-button"
              onclick={() => (pendingEndStream = false)}
            >
              Cancel
            </button>
            <button type="button" class="destructive-button" onclick={endLive}>
              Confirm
            </button>
          </div>
        {/snippet}
      </Modal>
    {/if}
  </div>
</div>
