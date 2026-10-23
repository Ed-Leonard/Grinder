<script lang="ts">
  import { onDestroy } from "svelte";
  import { Room, RoomEvent, type RemoteTrack } from "livekit-client";
  import { attachTrack } from "$lib/utils/attach-track";
  import { joinLiveStream, type LiveStreamItem } from "$lib/api/livestreams";
  import ProfileIcon from "$lib/components/svgs/profileicon.svelte";
  import Speaker from "$lib/components/svgs/clips/speaker.svelte";
  import { tooltip } from "$lib/utils/tooltip";
  import ErrorPopup from "$lib/components/utils/Error.svelte";
  import Viewer from "../svgs/livestream/viewer.svelte";
  import Clock from "../svgs/clock.svelte";
  import { formatTimer } from "$lib/utils/dateUtils.svelte";
  import { resolve } from "$app/paths";
  import Uprightarrow from "../svgs/uprightarrow.svelte";
  import ChatPanel from "$lib/components/streams/ChatPanel.svelte";
  import CommentIcon from "$lib/components/svgs/commenticon.svelte";

  let {
    item,
    active = false,
    preload = false,
    muted = $bindable(true),
  }: {
    item: LiveStreamItem;
    active?: boolean;
    preload?: boolean;
    muted?: boolean;
  } = $props();

  const CONNECT_DEBOUNCE = 250;

  let room = $state.raw<Room>();
  let connected = $state(false);
  let audioTrack = $state<RemoteTrack>();
  let videoTrack = $state<RemoteTrack>();
  let error = $state("");
  let ended = $state(false);
  let showChat = $state(false);

  let viewerCount = $state(0);

  $effect(() => {
    if (!room) return;
    const r = room;

    const update = () => {
      viewerCount = r.remoteParticipants.size;
      updateStartFromStreamer();
    };

    update();
    r.on(RoomEvent.Connected, update);
    r.on(RoomEvent.ParticipantConnected, update);
    r.on(RoomEvent.ParticipantDisconnected, update);

    return () => {
      r.off(RoomEvent.Connected, update);
      r.off(RoomEvent.ParticipantConnected, update);
      r.off(RoomEvent.ParticipantDisconnected, update);
    };
  });

  let startedAt = $state<number | undefined>(
    item.startedAt ? new Date(item.startedAt).getTime() : undefined,
  );
  let now = $state(Date.now());

  function updateStartFromStreamer() {
    if (!room) return;
    const streamer = room.remoteParticipants.get(String(item.streamer.id));
    if (streamer?.joinedAt) startedAt = streamer.joinedAt.getTime();
  }

  $effect(() => {
    if (!active) return;
    now = Date.now();
    const id = setInterval(() => (now = Date.now()), 1000);
    return () => clearInterval(id);
  });

  let elapsedSeconds = $derived(
    startedAt ? Math.max(0, Math.floor((now - startedAt) / 1000)) : 0,
  );
  let formattedTimer = $derived(formatTimer(elapsedSeconds));

  let credentials: { url: string; token: string } | undefined;
  let generation = 0;

  async function getCredentials() {
    if (!credentials) {
      credentials = await joinLiveStream(item.roomName);
    }
    return credentials;
  }

  async function prepare(gen: number) {
    if (room) return;
    try {
      const { url, token } = await getCredentials();
      if (gen !== generation) return;

      const newRoom = new Room();
      await newRoom.prepareConnection(url, token);
      if (gen !== generation) {
        newRoom.disconnect();
        return;
      }
      room = newRoom;
    } catch {}
  }

  function handleStreamEnded() {
    ended = true;
    room?.disconnect();
    room = undefined;
    connected = false;
    videoTrack = undefined;
    audioTrack = undefined;
  }

  async function connect(gen: number) {
    if (connected) return;
    try {
      const { url, token } = await getCredentials();
      if (gen !== generation) return;

      const activeRoom = room ?? new Room();
      room = activeRoom;

      activeRoom.on(RoomEvent.TrackSubscribed, (track) => {
        if (track.kind === "video") videoTrack = track;
        if (track.kind === "audio") audioTrack = track;
      });

      activeRoom.on(RoomEvent.TrackUnsubscribed, (track) => {
        if (track.kind === "video") videoTrack = undefined;
        if (track.kind === "audio") audioTrack = undefined;
      });

      activeRoom.on(RoomEvent.ParticipantDisconnected, (participant) => {
        if (participant.identity === String(item.streamer.id)) {
          handleStreamEnded();
        }
      });

      await activeRoom.connect(url, token);
      if (gen !== generation) {
        activeRoom.disconnect();
        room = undefined;
        connected = false;
        videoTrack = undefined;
        return;
    }

    // The streamer may have already left before we joined (stale feed
    // entry, or they ended in the gap between fetch and connect). In
    // that case there's no ParticipantDisconnected event to catch —
    // we have to check who's actually in the room right now.
    // Shouldn't be necessary once lingering stream problem is fixed 
    const streamerPresent = [...activeRoom.remoteParticipants.values()].some(
      (p) => p.identity === String(item.streamer.id),
    );
    if (!streamerPresent) {
      handleStreamEnded();
      return;
    }

      connected = true;
      error = "";
    } catch {
      if (gen === generation) {
        error = "Could not connect to this live stream.";
      }
    }
  }

  function disconnect() {
    room?.disconnect();
    room = undefined;
    connected = false;
    videoTrack = undefined;
    error = "";
  }

  $effect(() => {
    const wantActive = active;
    const wantPreload = preload;

    generation++;
    const gen = generation;

    if (!wantActive && !wantPreload) {
      disconnect();
      return;
    }

    const timer = setTimeout(() => {
      if (gen !== generation) return;
      if (wantActive) {
        connect(gen);
      } else {
        prepare(gen);
      }
    }, CONNECT_DEBOUNCE);

    return () => {
      clearTimeout(timer);
    };
  });

  onDestroy(disconnect);
</script>

<article
  class="relative h-full flex flex-col lg:flex-row lg:items-center lg:justify-center gap-0 lg:gap-3"
>
  <div
    class="relative flex-1 min-h-0 md:flex-1 md:min-w-0 md:max-w-sm md:h-full lg:rounded-lg overflow-hidden bg-black shadow-sm transition"
  >
    <button
      type="button"
      aria-label="Open chat"
      use:tooltip={showChat ? "Hide chat" : "Show chat"}
      class="z-10 absolute right-4 bottom-16 icon-button
                  {showChat
        ? 'bg-brand text-surface hover:bg-brand'
        : 'bg-[#242426]/30 hover:bg-[#242426] text-white'}"
      onclick={() => (showChat = !showChat)}
    >
      <CommentIcon />
    </button>
    {#if error}
      <div class="z-40 relative">
        <ErrorPopup {error} />
      </div>
    {/if}
    {#if ended}
      <div class="absolute inset-0 z-30 flex flex-col items-center justify-center gap-1 bg-black text-white text-center px-4">
        <span class="font-serif font-semibold text-lg">Stream ended</span>
        <span class="text-sm text-white/60">
          {item.streamer.displayName} isn't live anymore.
        </span>
        <a
          class=" bg-[#242426]/30 hover:bg-[#242426] inline-flex gap-2 text-white items-center secondary-button"
          href={resolve(`/tasks/${item.taskId}`)}
          aria-label="Go to task"
          use:tooltip={"Go to task"}
        >
          Go to task <Uprightarrow size={1} />
        </a>
      </div>
    {/if}
    <video
      class="h-full w-full object-contain"
      use:attachTrack={videoTrack}
      autoplay
      muted
      playsinline
    ></video>

    <audio
      use:attachTrack={audioTrack}
      autoplay
      volume={!active || muted ? 0 : 1}
    ></audio>

    <div
      class="absolute top-4 left-4 inline-flex gap-2 text-sm bg-[#242426]/30 rounded-lg p-2"
    >
      <div class="inline-flex gap-1 items-center">
        <Viewer size={1.25} />
        <span class="text-red-500">{viewerCount}</span>
      </div>
      ·
      <div class="inline-flex items-center gap-1 text-white">
        <Clock size={1.25} colour="CurrentColor" />{formattedTimer}
      </div>
    </div>
    <button
      type="button"
      class="absolute top-4 right-4 icon-button bg-[#242426]/30 hover:bg-[#242426] text-white"
      onclick={() => (muted = !muted)}
      aria-label={muted ? "Unmute" : "Mute"}
      use:tooltip={muted ? "Unmute" : "Mute"}
    >
      <Speaker size={1.5} {muted} />
    </button>

    <a
      class="absolute bottom-4 right-4 icon-button bg-[#242426]/30 hover:bg-[#242426] text-white"
      href={resolve(`/tasks/${item.taskId}`)}
      aria-label="Go to task"
      use:tooltip={"Go to task"}
    >
      <Uprightarrow size={1.5} />
    </a>

    <div
      class="pointer-events-none absolute inset-x-0 bottom-0 p-3 flex flex-col gap-2 text-white"
    >
      <div
        class="pointer-events-auto flex items-center gap-2 w-fit bg-[#242426]/30 rounded-lg px-3 py-2"
      >
        <ProfileIcon
          size={2.25}
          userId={item.streamer.id}
          alt={item.streamer.displayName}
        />
        <a
          href={resolve(`/profile/${item.streamer.id}`)}
          class="font-semibold text-sm">{item.streamer.displayName}</a
        >
      </div>
      {#if !ended}
      <div
        class="bg-[#242426]/30 rounded-lg px-4 py-2 w-fit max-w-full flex flex-col gap-1"
      >
        <h3 class="wrap-anywhere text-left text-white">{item.title}</h3>
        <p class="text-sm font-semibold">{item.taskTitle}</p>
      </div>
      {/if}
    </div>
  </div>
  <ChatPanel
    roomName={item.roomName}
    isOpen={showChat}
    onClose={() => (showChat = false)}
  />
</article>
