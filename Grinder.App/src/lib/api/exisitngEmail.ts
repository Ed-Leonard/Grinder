import { resolve } from "$app/paths";

export default async function checkEmailExists(
  email: string,
): Promise<boolean> {
  const response = await fetch(
    resolve(`/api/auth/check-email?email=${encodeURIComponent(email)}` as any),
    {
      method: "GET",
      headers: {
        "Content-Type": "application/json",
      },
    },
  );

  if (response.status === 409) {
    return true;
  } else {
    return false;
  }
}
