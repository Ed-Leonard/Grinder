import type { PageLoad } from "./$types";
import { error } from "@sveltejs/kit";
import { getFriends } from "$lib/api/follows";
import type { Friend } from "$lib/api/follows";
import { getUserProfile } from "$lib/api/profile";

export const load: PageLoad = async ({ params, fetch, depends }) => {
  const userId = Number(params.slug);
  depends(`app:friends:${userId}`);

  const friends: Friend[] = await getFriends(userId, fetch);
  const response = await getUserProfile(userId, fetch);

  if (!response.ok) {
    if (response.status == 404) {
      throw error(response.status, "User not found");
    } else {
      throw error(response.status, "Server Error");
    }
  }

  const profile = await response.json();

  return { friends, profile };
};
