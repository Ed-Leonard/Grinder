<script lang="ts">
  import { page } from "$app/state";
  import { goto } from "$app/navigation";
  import { resolve } from "$app/paths";
  import { onMount } from "svelte";
  import { fetchWithCsrf } from "$lib/csrf";
  import CodeVerification from "$lib/components/CodeVerification.svelte";
  import ErrorPopup from "$lib/components/utils/Error.svelte";
  import BasePage from "$lib/components/utils/BasePage.svelte";

  const CODE_TYPE = 0;
  let email = $derived(page.url.searchParams.get("email") ?? "");
  let error = $state(page.state.error ?? "");

  onMount(async () => {
    if (!email) {
      goto(resolve("/register"), {
        state: { error: "No email provided. Please register again." },
      });
      return;
    }

    try {
      const response = await fetchWithCsrf(
        resolve(`/api/verificationcode/verified?codeType=${CODE_TYPE}` as any),
        {
          method: "GET",
        },
      );
      if (!response.ok) {
        goto(resolve("/register"), {
          state: { error: "No email provided. Please register again." },
        });
      }
    } catch (err) {
      goto(resolve("/register"), {
        state: { error: "Something went wrong. Please register again." },
      });
    }
  });

  function handleSuccess() {
    goto(resolve("/login"), {
      state: { success: "Registration successful. Please log in." },
    });
  }
</script>

<BasePage maxWidth="max-w-lg" class="bg-surface! md:bg-surface-alt! h-dvh">
  <h1 class="mb-6">Grinder</h1>
  {#key error}
    {#if error}
      <ErrorPopup {error} />
    {/if}
  {/key}

  <div class="card">
    <a class="cancel-button" href={resolve("/")}> Cancel </a>
    <h2>Verify Email</h2>
    <CodeVerification
      {email}
      type={CODE_TYPE}
      onSuccess={handleSuccess}
      confirmLabel="Confirm Registration"
    />
  </div>
</BasePage>
