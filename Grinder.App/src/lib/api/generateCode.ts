import { fetchWithCsrf } from "$lib/csrf";
import { resolve } from "$app/paths";

export async function generateCode(
  type: number,
  email?: string,
): Promise<string | null> {
  try {
    await fetchWithCsrf(resolve("/api/verificationcode/generate" as any), {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email, type }),
    });
    return null;
  } catch (err) {
    return "Failed to send code";
  }
}
