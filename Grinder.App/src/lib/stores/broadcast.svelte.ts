import {
  createLocalAudioTrack,
  createLocalVideoTrack,
  Room,
  RoomEvent,
  Track,
  type LocalAudioTrack,
  type LocalVideoTrack,
  type RemoteTrack,
} from "livekit-client";
import { fetchWithCsrf, getCachedCsrfToken } from "$lib/csrf";
import { TaskVisibility } from "$lib/types";
import { resolve } from "$app/paths";

export type Device = { deviceId: string; label: string };
export type RemoteMedia = {
  id: string;
  track: RemoteTrack;
  participant: string;
};

export type GoLiveParams = {
  title: string;
  taskId: number;
  taskTitle: string;
  visibility: TaskVisibility;
};

export type ChatMessage = { id: string; user: string; text: string };

/**
 * Holds everything that needs to survive the navigation from the "start a
 * stream" page to the "manage the live stream" page: the Room instance,
 * local/remote tracks, and the stream's metadata. Because SvelteKit
 * navigation is client-side, this module-level state (and the live
 * LocalVideoTrack/Room objects inside it) is preserved across the route
 * change — nothing gets torn down and recreated.
 */
class BroadcastStore {
  // Room/session state
  room = $state<Room>();
  roomName = $state("");
  connected = $state(false);
  viewerCount = $state(0);
  remoteTracks = $state<RemoteMedia[]>([]);
  chatMessages = $state<ChatMessage[]>([]);

  // Stream metadata
  title = $state("");
  taskId = $state<number | "">("");
  taskTitle = $state("");
  visibility = $state<TaskVisibility>(TaskVisibility.Public);

  // Devices
  cameras = $state<Device[]>([]);
  microphones = $state<Device[]>([]);
  selectedCameraId = $state("");
  selectedMicrophoneId = $state("");

  // Local tracks
  previewTrack = $state<LocalVideoTrack>();
  previewAudio = $state<LocalAudioTrack>();
  micMuted = $state(false);
  cameraMuted = $state(false);

  error = $state("");

  async enumerateDevices() {
    const devices = await navigator.mediaDevices.enumerateDevices();

    this.cameras = devices
      .filter((device) => device.kind === "videoinput")
      .map((device) => ({
        deviceId: device.deviceId,
        label: device.label || `Camera ${device.deviceId.slice(0, 5)}`,
      }));

    this.microphones = devices
      .filter((device) => device.kind === "audioinput")
      .map((device) => ({
        deviceId: device.deviceId,
        label: device.label || `Microphone ${device.deviceId.slice(0, 5)}`,
      }));

    this.selectedCameraId ||= this.cameras[0]?.deviceId ?? "";
  }

  async requestDevices() {
    this.error = "";

    const describeError = (reason: unknown, kind: "camera" | "microphone") => {
      const name = reason instanceof DOMException ? reason.name : "";
      if (name === "NotFoundError")
        return `No ${kind} was found on this device.`;
      if (name === "NotReadableError")
        return `${kind[0].toUpperCase()}${kind.slice(1)} is already in use by another application.`;
      if (name === "NotAllowedError" || name === "SecurityError")
        return `${kind[0].toUpperCase()}${kind.slice(1)} permission is required.`;
      return `Could not access ${kind}.`;
    };

    let gotVideo = false;
    let gotAudio = false;
    const errors: string[] = [];

    try {
      const videoStream = await navigator.mediaDevices.getUserMedia({
        video: true,
      });
      videoStream.getTracks().forEach((track) => track.stop());
      gotVideo = true;
    } catch (reason) {
      console.error("Camera permission failed", reason);
      errors.push(describeError(reason, "camera"));
    }

    try {
      const audioStream = await navigator.mediaDevices.getUserMedia({
        audio: true,
      });
      audioStream.getTracks().forEach((track) => track.stop());
      gotAudio = true;
    } catch (reason) {
      console.error("Microphone permission failed", reason);
      errors.push(describeError(reason, "microphone"));
    }

    if (!gotVideo && !gotAudio) {
      this.error = errors.join(" ");
      return;
    }

    this.error = gotVideo && gotAudio ? "" : errors.join(" ");

    await this.enumerateDevices();

    if (gotVideo) {
      await this.startPreview();
      if (gotAudio) {
        await this.enableAudioPreview();
      }
    }
  }

  private previewRequestId: number = 0;

  async startPreview() {
    const requestId = ++this.previewRequestId;
    const previousTrack = this.previewTrack;

    if (!this.selectedCameraId) {
      previousTrack?.stop();
      this.previewTrack = undefined;
      return;
    }

    let newTrack;
    try {
      newTrack = await createLocalVideoTrack({
        deviceId: this.selectedCameraId || undefined,
      });
    } catch (err) {
      console.error("Failed to create video track", err);
      this.error = "Could not access camera.";
      return;
    }

    if (requestId !== this.previewRequestId) {
      newTrack.stop();
      return;
    }

    if (this.connected && this.room) {
      try {
        if (previousTrack) {
          await this.room.localParticipant.unpublishTrack(previousTrack);
        }
        await this.room.localParticipant.publishTrack(newTrack);
        this.cameraMuted = false;
      } catch (reason) {
        console.error("Failed to swap camera while live", reason);
        this.error = "Could not switch camera while live.";
      }
    }

    previousTrack?.stop();
    this.previewTrack = newTrack;
  }

  async enableAudioPreview() {
    const previousAudio = this.previewAudio;

    if (!this.selectedMicrophoneId) {
      if (this.connected && this.room && previousAudio) {
        try {
          await this.room.localParticipant.unpublishTrack(previousAudio);
        } catch (reason) {
          console.error("Failed to unpublish audio", reason);
        }
      }
      previousAudio?.stop();
      this.previewAudio = undefined;
      this.micMuted = false;
      this.#stopLevelMeter();
      return;
    }

    const newAudio = await createLocalAudioTrack({
      deviceId: this.selectedMicrophoneId,
    });

    if (this.connected && this.room) {
      try {
        if (previousAudio) {
          await this.room.localParticipant.unpublishTrack(previousAudio);
        }
        await this.room.localParticipant.publishTrack(newAudio);
        this.micMuted = false;
      } catch (reason) {
        console.error("Failed to publish audio while live", reason);
        this.error = "Could not enable microphone while live.";
      }
    }

    previousAudio?.stop();
    this.previewAudio = newAudio;
    this.#startLevelMeter(newAudio);
  }

  async toggleMic() {
    if (!this.previewAudio) return;
    this.micMuted = !this.micMuted;
    await (this.micMuted
      ? this.previewAudio.mute()
      : this.previewAudio.unmute());
    if (this.micMuted) this.audioLevel = 0;
  }

  async toggleCamera() {
    if (!this.previewTrack) return;
    this.cameraMuted = !this.cameraMuted;
    await (this.cameraMuted
      ? this.previewTrack.mute()
      : this.previewTrack.unmute());
  }

  updateViewerCount() {
    this.viewerCount = this.room?.remoteParticipants.size ?? 0;
  }

  handleTrackSubscribed = (
    track: RemoteTrack,
    publication: { trackSid: string },
    participant: { identity: string },
  ) => {
    if (track.kind !== Track.Kind.Video && track.kind !== Track.Kind.Audio) {
      return;
    }

    this.remoteTracks = [
      ...this.remoteTracks,
      { id: publication.trackSid, track, participant: participant.identity },
    ];
  };

  handleTrackUnsubscribed = (track: RemoteTrack) => {
    track.detach();
    this.remoteTracks = this.remoteTracks.filter(
      (item) => item.track !== track,
    );
  };

  /** Fetches a token, connects to the room, and publishes tracks. Returns success. */
  async goLive({ title, taskId, taskTitle, visibility }: GoLiveParams) {
    this.error = "";
    this.title = title;
    this.taskId = taskId;
    this.taskTitle = taskTitle;
    this.visibility = visibility;

    try {
      const tokenResponse = await fetchWithCsrf(
        resolve("/api/livestreams/token" as any),
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ title, taskId, visibility }),
        },
      );
      // added by claude
      if (tokenResponse.status === 409) {
        throw new Error(
          "You're already live. End your current stream before starting another.",
        );
      }

      if (!tokenResponse.ok) {
        throw new Error("Could not obtain a LiveKit access token.");
      }

      const tokenData = (await tokenResponse.json()) as {
        url: string;
        token: string;
        roomName: string;
      };

      const room = new Room();
      room.on(RoomEvent.TrackSubscribed, this.handleTrackSubscribed);
      room.on(RoomEvent.TrackUnsubscribed, this.handleTrackUnsubscribed);
      room.on(RoomEvent.ParticipantConnected, () => this.updateViewerCount());
      room.on(RoomEvent.ParticipantDisconnected, () =>
        this.updateViewerCount(),
      );

      this.roomName = tokenData.roomName;
      await room.connect(tokenData.url, tokenData.token);

      let videoTrack = this.previewTrack;
      if (!videoTrack && this.selectedCameraId) {
        videoTrack = await createLocalVideoTrack({
          deviceId: this.selectedCameraId,
        });
      }
      if (videoTrack) {
        await room.localParticipant.publishTrack(videoTrack);
        this.previewTrack = videoTrack;
      }

      if (this.selectedMicrophoneId) {
        const audioTrack =
          this.previewAudio ??
          (await createLocalAudioTrack({
            deviceId: this.selectedMicrophoneId || undefined,
          }));
        await room.localParticipant.publishTrack(audioTrack);
        this.previewAudio = audioTrack;
      }

      this.room = room;
      this.roomName = tokenData.roomName;
      this.micMuted = false;
      this.cameraMuted = false;
      this.connected = true;
      this.updateViewerCount();
      return true;
    } catch (reason) {
      this.error =
        reason instanceof Error
          ? reason.message
          : "Could not connect to LiveKit.";
      await this.endLive();
      return false;
    }
  }

  /** Ends the stream but keeps the camera preview running (back to setup state). */
  async endLive() {
    await this.#notifyEnded();
    this.room?.disconnect();
    this.room = undefined;
    this.roomName = "";
    this.connected = false;
    this.remoteTracks = [];
    this.viewerCount = 0;
    this.micMuted = false;
    this.cameraMuted = false;
    this.chatMessages = [];

    this.previewAudio?.stop();
    this.previewAudio = undefined;
    this.#stopLevelMeter();

    if (this.previewTrack) {
      await this.startPreview();
    }
  }

  /** Send chat to the room */
  async sendChat(text: string) {
    const local = this.room?.localParticipant;
    if (!local) throw new Error("Not connected");

    await local.sendText(text, { topic: "chat" });
    this.chatMessages.push({
      id: crypto.randomUUID(),
      user: local.name || "",
      text,
    });
  }

  /** Full teardown — stops everything. Used when abandoning the flow entirely. */
  disposeAll() {
    void this.#notifyEnded();
    this.room?.disconnect();
    this.room = undefined;
    this.roomName = "";
    this.previewTrack?.stop();
    this.previewTrack = undefined;
    this.previewAudio?.stop();
    this.previewAudio = undefined;
    this.#stopLevelMeter();
    this.connected = false;
    this.remoteTracks = [];
    this.viewerCount = 0;
    this.chatMessages = [];
  }

  // Equalizer stuff, claude codium credited, touch if dare
  audioLevel = $state(0);

  #audioCtx?: AudioContext;
  #analyser?: AnalyserNode;
  #rafId?: number;

  #startLevelMeter(track: LocalAudioTrack) {
    this.#stopLevelMeter();

    this.#audioCtx = new AudioContext();
    const source = this.#audioCtx.createMediaStreamSource(
      new MediaStream([track.mediaStreamTrack]),
    );
    this.#analyser = this.#audioCtx.createAnalyser();
    this.#analyser.fftSize = 512;
    this.#analyser.smoothingTimeConstant = 0.8;
    source.connect(this.#analyser);

    const data = new Uint8Array(this.#analyser.frequencyBinCount);

    const tick = () => {
      this.#analyser!.getByteTimeDomainData(data);
      let sumSquares = 0;
      for (const value of data) {
        const normalized = (value - 128) / 128;
        sumSquares += normalized * normalized;
      }
      const rms = Math.sqrt(sumSquares / data.length);

      // Boost and clamp so normal mic levels use the full 0–1 range
      const boosted = Math.min(1, rms * 2); // tweak the multiplier to taste
      this.audioLevel = boosted;

      this.#rafId = requestAnimationFrame(tick);
    };
    tick();
  }

  #stopLevelMeter() {
    if (this.#rafId) cancelAnimationFrame(this.#rafId);
    this.#rafId = undefined;
    this.#audioCtx?.close();
    this.#audioCtx = undefined;
    this.#analyser = undefined;
    this.audioLevel = 0;
  }

  /** Tells the API to close the room. `keepalive` allows this to survive a page unload. */
  async #notifyEnded() {
    const roomName = this.roomName;
    this.roomName = "";
    if (!roomName) return;

    const url = resolve(`/api/livestreams/${roomName}` as any);
    const token = getCachedCsrfToken();
    try {
      if (!token) {
        await fetchWithCsrf(url, { method: "DELETE", keepalive: true });
        return;
      }

      await fetch(url, {
        method: "DELETE",
        credentials: "include",
        keepalive: true,
        headers: { "X-CSRF-TOKEN": token },
      });
    } catch (reason) {
      // Local media must still be released if the browser cannot notify the API.
      console.warn(
        "Could not notify the server that the livestream ended.",
        reason,
      );
    }
  }
}
export const broadcast = new BroadcastStore();
