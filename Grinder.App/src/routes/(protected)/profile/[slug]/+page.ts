import type { PageLoad } from "./$types";
import { error } from "@sveltejs/kit";
import { getUserProfile } from "$lib/api/profile";

export const load: PageLoad = async ({ params, fetch, depends }) => {
  const userId = Number(params.slug);

  depends(`app:profile:${userId}`);

  const response = await getUserProfile(userId, fetch);

  if (!response.ok) {
    if (response.status == 404) {
      throw error(response.status, "User not found");
    } else {
      throw error(response.status, "Server Error");
    }
  }

  const profile = await response.json();

  return { profile };
};
