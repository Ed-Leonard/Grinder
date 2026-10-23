<script lang="ts">
  import { goto } from "$app/navigation";
  import Form from "$lib/components/Form.svelte";
  import type { FormData } from "$lib/types";
  import { resolve } from "$app/paths";
  import { fetchWithCsrf } from "$lib/csrf";
  import ErrorPopup from "$lib/components/utils/Error.svelte";
  import { page } from "$app/state";
  import BasePage from "$lib/components/utils/BasePage.svelte";

  let loading = $state(false);
  let error = $state(page.state.error ?? "");

  /**
   * Checks null input and matching passwords then switches to log in
   * Requires further validation checks with backend etc.
   */
  async function handleRegister({ email, name, password, country }: FormData) {
    error = "";

    // create user in database
    try {
      loading = true;
      error = "";
      const response = await fetchWithCsrf(
        resolve(`/api/auth/register` as any),
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({
            email,
            displayName: name,
            password,
            country,
          }),
        },
      );

      if (!response.ok) {
        if (response.status === 409) {
          throw new Error("Email already in use");
        } else {
          throw new Error("Unknown error");
        }
      }

      error = "";
      goto(
        resolve(
          `/email-verification?email=${encodeURIComponent(email)}` as any,
        ),
      );
    } catch (err) {
      error = "Failed to register: " + (err as Error).message;
      console.error(err);
    } finally {
      loading = false;
    }
  }
</script>

<BasePage maxWidth="max-w-lg" class="bg-surface! md:bg-surface-alt! h-dvh">
  <h1 class="mb-6">Grinder</h1>
  {#key error}
    {#if error}
      <ErrorPopup {error} />
    {/if}
  {/key}
  <Form registering={true} onsubmit={handleRegister} bind:loading />
</BasePage>
