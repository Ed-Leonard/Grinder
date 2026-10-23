import { resolve } from "$app/paths";
import { fetchWithCsrf } from "$lib/csrf";
import type { SearchResponse } from "$lib/types";

export async function search(
  query: string,
  page = 1,
  limit = 20,
): Promise<SearchResponse> {
  const response = await fetchWithCsrf(
    resolve(
      `/api/search?query=${encodeURIComponent(query)}&page=${page}&limit=${limit}` as any,
    ),
    {
      credentials: "include",
    },
  );

  if (!response.ok) {
    throw new Error("Failed to fetch search results");
  }

  const data = (await response.json()) as SearchResponse;
  return data;
}
