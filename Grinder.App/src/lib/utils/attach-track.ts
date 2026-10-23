import type { Track, RemoteTrack } from "livekit-client";

export function attachTrack(
  node: HTMLMediaElement,
  track: Track | RemoteTrack | undefined,
) {
  let currentTrack = track;
  currentTrack?.attach(node);

  return {
    update(nextTrack: Track | RemoteTrack | undefined) {
      currentTrack?.detach(node);
      currentTrack = nextTrack;
      currentTrack?.attach(node);
    },
    destroy: () => currentTrack?.detach(node),
  };
}
