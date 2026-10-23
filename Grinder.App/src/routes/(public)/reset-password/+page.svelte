<script lang="ts">
  import { page } from "$app/state";
  import { goto } from "$app/navigation";
  import { fetchWithCsrf } from "$lib/csrf";
  import { resolve } from "$app/paths";
  import { onMount } from "svelte";
  import ErrorPopup from "$lib/components/utils/Error.svelte";
  import SuccessPopup from "$lib/components/utils/Success.svelte";
  import PasswordField from "$lib/components/FormFields/PasswordField.svelte";
  import CodeVerification from "$lib/components/CodeVerification.svelte";
  import EmailField from "$lib/components/FormFields/EmailField.svelte";
  import BasePage from "$lib/components/utils/BasePage.svelte";

  let CODE_TYPE = 2;
  let step: "email" | "code" | "password" = $state("email");

  let email = $state("");
  let password = $state("");
  let confirmPassword = $state("");
  let loading = $state(false);
  let success = $state(page.state.success ?? "");
  let error = $state("");
  let submitted = $state(false);
  let matchingPasswords = $state<HTMLInputElement | null>(null);

  onMount(async () => {
    try {
      loading = true;
      const response = await fetchWithCsrf(
        resolve(`/api/verificationcode/verified?codeType=${CODE_TYPE}` as any),
        {
          method: "GET",
        },
      );

      if (response.ok) {
        step = "password";
      } else {
        step = "email";
      }
    } catch (err) {
      goto(resolve("/login"), {
        state: { error: "Something went wrong. Please try again." },
      });
    } finally {
      loading = false;
    }
  });

  $effect(() => {
    if (matchingPasswords) {
      if (confirmPassword && password !== confirmPassword) {
        matchingPasswords.setCustomValidity("Passwords do not match");
      } else {
        matchingPasswords.setCustomValidity("");
      }
    }
  });

  async function handleSendCode(e: SubmitEvent) {
    e.preventDefault();
    submitted = true;
    error = "";
    success = "";

    const form = e.currentTarget as HTMLFormElement;
    if (!form.checkValidity()) {
      return;
    }

    try {
      loading = true;
      const response = await fetch(
        resolve("/api/auth/forgot-password" as any),
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({ email }),
        },
      );

      if (!response.ok) {
        const errorData = await response.json();
        throw new Error(errorData.message || "Failed to send reset code");
      }

      success = "Password reset email sent";
      step = "code";
      submitted = false;
    } catch (err) {
      error = "Failed to send reset code";
      console.error(err);
    } finally {
      loading = false;
    }
  }

  async function handleReset(e: SubmitEvent) {
    e.preventDefault();
    submitted = true;
    error = "";

    const form = e.currentTarget as HTMLFormElement;
    if (!form.checkValidity()) {
      return;
    }

    try {
      loading = true;
      const response = await fetchWithCsrf(
        resolve("/api/auth/reset-password" as any),
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            newPassword: password,
          }),
        },
      );

      if (response.ok) {
        goto(resolve("/login"), {
          state: { success: "Password reset successfully" },
        });
        return;
      }
      if (response.status == 422) {
        step = "email";
        error = "Code is no longer valid, please ask for a new code.";
        password = "";
        confirmPassword = "";
        submitted = false;

        return;
      }
      error = "Failed to reset password. Please try again.";
    } catch (err) {
      error = "Failed to reset password. Please try again.";
    } finally {
      loading = false;
    }
  }

  async function handleCancel() {
    try {
      await fetchWithCsrf(resolve("/api/auth/cancel-reset" as any), {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email }),
      });
    } catch (err) {
      error = "Failed to cancel reset password";
    }

    goto(resolve("/login"));
  }
</script>

<BasePage
  maxWidth={step === "email" ? "max-w-md" : "max-w-lg"}
  class="bg-surface! md:bg-surface-alt! h-dvh"
>
  <h1 class="mb-6">Grinder</h1>
  {#key success}
    {#if success}
      <SuccessPopup {success} />
    {/if}
  {/key}
  {#key error}
    {#if error}
      <ErrorPopup {error} />
    {/if}
  {/key}
  <div class="card">
    <button class="cancel-button" onclick={handleCancel}>Cancel</button>
    <h2 class="mb-4">
      {step === "email"
        ? "Reset Your Password"
        : step === "code"
          ? "Enter Reset Code"
          : "Set New Password"}
    </h2>
    {#if step === "email"}
      <form novalidate onsubmit={handleSendCode}>
        <EmailField bind:email {submitted} {loading} />
        <div class="w-full">
          <button type="submit" class="primary-button w-full mt-4"
            >Get reset code</button
          >
        </div>
      </form>
    {:else if step === "code"}
      <CodeVerification
        {email}
        type={CODE_TYPE}
        confirmLabel="Reset Password"
        onSuccess={() => {
          ((step = "password"), (success = ""));
        }}
      />
    {:else if step === "password"}
      <form class="flex flex-col gap-4" onsubmit={handleReset} novalidate>
        <PasswordField
          {submitted}
          {loading}
          bind:password
          pattern={"^(?=.*[0-9])(?=.*[a-z])(?=.*[A-Z])(?=.*[^a-zA-Z0-9]).{8,}$"}
          errorMessage="Your password must be at least 8 characters long, and contain at least one of each uppercase, lowercase, numbers and special characters"
        />
        <PasswordField
          id="confirmPassword"
          label="Confirm Password"
          {submitted}
          {loading}
          bind:password={confirmPassword}
          errorMessage="Passwords do not match"
          bind:ref={matchingPasswords}
        />
        <button type="submit" class="primary-button" disabled={loading}>
          Reset Password
        </button>
      </form>
    {/if}
  </div>
</BasePage>
