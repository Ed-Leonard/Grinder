import { resolve } from "$app/paths";
import { get } from "svelte/store";
import { profilePictureVersion } from "$lib/stores/profilePicture";

export function profilePictureUrl(userId: number): string {
  const v = get(profilePictureVersion);
  return resolve(`/api/media/${userId}/profile-picture?v=${v}` as any);
}

export function taskAttachmentUrl(attachmentId: number): string {
  const base = resolve(`/api/media/task-attachments/${attachmentId}` as any);
  return base;
}
