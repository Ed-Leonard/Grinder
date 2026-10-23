import { resolve } from "$app/paths";
import { fetchWithCsrf } from "$lib/csrf";

async function getUserProfile(
  userId: number,
  fetch: typeof globalThis.fetch = globalThis.fetch,
) {
  const response = await fetch(resolve(`/api/user/${userId}` as any), {
    credentials: "include",
  });
  return response;
}

export interface ProfileUpdatePayload {
  email: string;
  displayName: string;
  country: string;
}

async function updateProfile(payload: ProfileUpdatePayload): Promise<Response> {
  return fetchWithCsrf(resolve(`/api/user/profile` as any), {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    credentials: "include",
    body: JSON.stringify(payload),
  });
}

async function uploadProfilePicture(file: File): Promise<Response> {
  const formData = new FormData();
  formData.append("file", file);

  return fetchWithCsrf(resolve(`/api/user/profile/upload` as any), {
    method: "PUT",
    body: formData,
  });
}

export { getUserProfile, uploadProfilePicture, updateProfile };
