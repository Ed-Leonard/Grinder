<script lang="ts">
  import { page } from "$app/state";
  import { goto } from "$app/navigation";
  import { fetchWithCsrf } from "$lib/csrf";
  import { resolve } from "$app/paths";
  import PasswordField from "$lib/components/FormFields/PasswordField.svelte";
  import ErrorPopup from "$lib/components/utils/Error.svelte";
  import CodeVerification from "$lib/components/CodeVerification.svelte";
  import { generateCode } from "$lib/api/generateCode";
  import { onMount } from "svelte";
  import { user } from "$lib/stores/auth";
  import BasePage from "$lib/components/utils/BasePage.svelte";

  const CODE_TYPE = 1;

  let email = $user?.email ?? "";

  let submitted = $state(false);
  let loading = $state(false);
  let error = $state(page.state?.error ?? "");

  let oldPassword = $state("");
  let oldPasswordError = $state("");
  let oldPasswordElement = $state<HTMLInputElement | null>(null);

  let newPassword = $state("");
  let confirmPassword = $state("");
  let matchingPasswords = $state<HTMLInputElement | null>(null);
  var invalidPasswordError =
    "Password must be at least 8 characters long including at least one of each uppercase, lowercase, numbers and special characters.";

  let step: "code" | "password" = $state("code");

  onMount(async () => {
    try {
      loading = true;

      const response = await fetchWithCsrf(
        resolve(`/api/verificationcode/verified?codeType=${CODE_TYPE}` as any),
        {
          method: "GET",
          credentials: "include",
        },
      );

      if (response.ok) {
        step = "password";
      } else {
        step = "code";
      }
    } catch (err) {
      error = "Something went wrong";
      step = "code";
    } finally {
      loading = false;
    }
  });

  async function sendCode() {
    loading = true;
    const err = await generateCode(CODE_TYPE, email);
    if (err) error = err;
    loading = false;
  }

  async function updatePassword(e: SubmitEvent) {
    e.preventDefault();
    submitted = true;
    error = "";
    oldPasswordElement?.setCustomValidity("");

    const form = e.currentTarget as HTMLFormElement;
    const elements = Array.from(form.elements) as HTMLInputElement[];

    if (newPassword !== confirmPassword) {
      newPassword = "";
      confirmPassword = "";
      matchingPasswords?.setCustomValidity("Passwords do not match");
    }

    try {
      loading = true;

      const response = await fetchWithCsrf(
        resolve("/api/user/update-password" as any),
        {
          method: "PUT",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            oldPassword,
            newPassword,
            confirmPassword,
          }),
        },
      );

      elements.forEach((el) => {
        if (!el.checkValidity()) {
          if (el.id === "newPassword") newPassword = "";
          if (el.id === "confirmPassword") confirmPassword = "";
        }
      });

      if (!response.ok) {
        if (response.status === 422) {
          error = "No longer verified, please enter new code.";
          step = "code";
          submitted = false;
          oldPassword = "";
          newPassword = "";
          confirmPassword = "";
          sendCode();
        } else if (response.status === 401) {
          oldPasswordError = "Old password does not match password on file";
          oldPasswordElement?.setCustomValidity(oldPasswordError);
          oldPassword = "";
        } else if (response.status === 400) {
          const data = await response.json().catch(() => null);
          if (
            data?.message != invalidPasswordError &&
            data?.message != "Passwords do not match"
          ) {
            error = data?.message || "Invalid details";
          }
        } else {
          error = "Something went wrong";
        }
        newPassword = "";
        confirmPassword = "";
        return;
      }

      goto(resolve("/profile"), {
        state: { success: "New password updated successfully" },
      });
    } catch (err) {
      error = "Password update failed";
    } finally {
      loading = false;
    }
  }

  $effect(() => {
    if (oldPasswordElement) {
      oldPasswordElement.setCustomValidity(oldPasswordError);
    }
  });

  $effect(() => {
    if (matchingPasswords) {
      if (confirmPassword && newPassword !== confirmPassword) {
        matchingPasswords.setCustomValidity("Passwords do not match");
      } else {
        matchingPasswords.setCustomValidity("");
      }
    }
  });
</script>

<BasePage maxWidth="max-w-lg" class="bg-surface! md:bg-surface-alt!">
  {#key error}
    {#if error}
      <ErrorPopup {error} />
    {/if}
  {/key}
  <div class="card">
    <button class="cancel-button" onclick={() => goto(resolve("/profile"))}>
      Cancel
    </button>
    <h2 class="mb-6">
      {step === "code" ? "Confirm Identity" : "Enter New Password"}
    </h2>
    {#if step === "code"}
      <CodeVerification
        {email}
        type={CODE_TYPE}
        onSuccess={() => (step = "password")}
      />
    {:else}
      <form onsubmit={updatePassword} novalidate class="flex flex-col gap-4">
        <PasswordField
          id="currentPassword"
          label="Old Password"
          bind:password={oldPassword}
          {loading}
          bind:ref={oldPasswordElement}
          errorMessage={oldPasswordError}
          {submitted}
        />
        <PasswordField
          id="newPassword"
          label="New Password"
          bind:password={newPassword}
          {loading}
          pattern={"^(?=.*[0-9])(?=.*[a-z])(?=.*[A-Z])(?=.*[^a-zA-Z0-9]).{8,}$"}
          errorMessage={invalidPasswordError}
          {submitted}
        />

        <PasswordField
          id="confirmPassword"
          label="Confirm New Password"
          bind:password={confirmPassword}
          {loading}
          errorMessage="Passwords do not match"
          bind:ref={matchingPasswords}
          {submitted}
        />
        <button type="submit" disabled={loading} class="primary-button">
          {loading ? "Updating..." : "Update Password"}
        </button>
      </form>
    {/if}
  </div>
</BasePage>
