import { writable } from "svelte/store";
import { goto } from "$app/navigation";
import { fetchWithCsrf } from "$lib/csrf";
import { browser } from "$app/environment";
import { resolve } from "$app/paths";
import { getUser } from "$lib/requests/user";

export const user = writable<{
  id: number;
  displayName: string;
  email: string;
  country: string;
} | null>(null);

/**
 * checks if the user is authenticated by running a get request, if authenticated it returns users profile details and is stored, otherwise removes user store if it exists
 */
export async function checkAuth() {
  try {
    const response = await getUser();
    user.set(response?.ok ? await response.json() : null);
  } catch {
    user.set(null);
  }
}

/**
 * Logs user out by calling api to remove cookie and wipes user store to update front end state
 *
 * @param locals - The place where user is stored if authenticated
 */
export async function logout() {
  // make sure function is called from browser context
  // goto will error otherwise
  if (!browser) return;
  await fetchWithCsrf(resolve("/api/auth/logout" as any), {
    method: "POST",
    credentials: "include",
  });

  user.set(null);
  goto(resolve("/login"));
}
