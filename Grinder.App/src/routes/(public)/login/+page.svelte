<script lang="ts">
  import { page } from "$app/state";
  import Form from "$lib/components/Form.svelte";
  import type { FormData } from "$lib/types";
  import { resolve } from "$app/paths";
  import { fetchWithCsrf } from "$lib/csrf";
  import { goto } from "$app/navigation";
  import SuccessPopup from "$lib/components/utils/Success.svelte";
  import BasePage from "$lib/components/utils/BasePage.svelte";
  import { checkAuth, user } from "$lib/stores/auth";
  import { get } from "svelte/store";

  let loading = $state(false);
  let success = $derived(page.state.success ?? "");
  let error = $state("");

  /**
   * Checks null input and matching passwords then switches to log in
   * Requires further validation checks with backend etc.
   */
  async function handleLogin({
    email,
    password,
  }: Pick<FormData, "email" | "password">) {
    try {
      loading = true;
      error = "";
      const response = await fetchWithCsrf(resolve(`/api/auth/login` as any), {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          email,
          password,
        }),
      });

      if (response.status === 401 || response.status == 400) {
        const message = await response.text();
        if (message === "Account is not validated yet, check your emails.") {
          goto(
            resolve(
              `/email-verification?email=${encodeURIComponent(email)}` as any,
            ),
            {
              state: { error: message },
            },
          );
          return;
        }
        error = "Invalid email or password";
        return;
      } else if (response.status === 500) {
        error = "Unknown error";
        return;
      }

      await checkAuth();
      await goto(resolve(`/profile/${get(user)?.id}`));
    } catch (err) {
      error = "Failed to sign in: Unknown error";
      console.error(err);
    } finally {
      loading = false;
    }
  }
</script>

<BasePage maxWidth="max-w-md" class="bg-surface! md:bg-surface-alt! h-dvh">
  <h1 class="mb-6">Grinder</h1>
  {#key success}
    {#if success}
      <SuccessPopup {success} />
    {/if}
  {/key}
  <Form
    registering={false}
    onsubmit={handleLogin}
    bind:loading
    loginError={error}
  />
</BasePage>
