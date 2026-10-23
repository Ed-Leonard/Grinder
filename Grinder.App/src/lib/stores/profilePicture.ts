import { writable } from "svelte/store";

export const profilePictureVersion = writable(Date.now());

export function bumpProfilePictureVersion() {
  profilePictureVersion.set(Date.now());
}
