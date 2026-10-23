import { fetchWithCsrf } from "$lib/csrf";
import { resolve } from "$app/paths";

async function getUser() {
  const response = await fetchWithCsrf(resolve(`/api/user/profile` as any), {
    method: "GET",
    credentials: "include",
  });
  return response;
}

export { getUser };
