import { resolve } from "$app/paths";
import { fetchWithCsrf } from "$lib/csrf";

export interface Reaction {
  id: number;
  name: string;
  fileName: string;
}

export interface ReactionResponse {
  reaction: Reaction;
  count: number;
  reactedByCurrentUser: boolean;
}

//Fetches all available reactions from the API
export async function fetchReactions(): Promise<Reaction[]> {
  const response = await fetchWithCsrf(resolve(`/api/reactions` as any), {
    method: "GET",
    credentials: "include",
  });

  if (!response.ok) {
    throw new Error("Failed to fetch reactions");
  }

  return await response.json();
}

export async function toggleTaskReaction(
  taskId: number,
  reactionId: number,
): Promise<ReactionResponse> {
  const response = await fetchWithCsrf(
    resolve(`/api/tasks/${taskId}/reactions/${reactionId}` as any),
    {
      method: "POST",
      credentials: "include",
    },
  );

  if (!response.ok) {
    throw new Error("Failed to toggle reaction for task");
  }

  return await response.json();
}
