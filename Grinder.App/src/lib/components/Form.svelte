<script lang="ts">
  import { goto } from "$app/navigation";
  import { resolve } from "$app/paths";
  import EmailField from "$lib/components/FormFields/EmailField.svelte";
  import type { FormData } from "$lib/types";
  import CountryField from "$lib/components/FormFields/CountryField.svelte";
  import NameField from "$lib/components/FormFields/NameField.svelte";
  import PasswordField from "$lib/components/FormFields/PasswordField.svelte";
  import checkEmailExists from "$lib/api/exisitngEmail";

  let {
    registering = true,
    onsubmit,
    loading = $bindable(false),
    loginError = "",
  }: {
    registering?: boolean;
    onsubmit: (data: FormData) => Promise<void>;
    loading?: boolean;
    loginError?: string;
  } = $props();

  let dupeEmailError = $state("");

  let email = $state("");
  let password = $state("");
  let displayName = $state("");
  let confirmPassword = $state("");
  let country = $state("");
  let matchingPasswords = $state<HTMLInputElement | null>(null);

  let submitted = $state(false);

  /**
   * Does some client side form validation checks and passes the information up to parent if they are valid
   */
  async function handleSubmit(e: SubmitEvent) {
    e.preventDefault();
    submitted = true;
    loading = true;

    dupeEmailError = "";
    loginError = "";

    const form = e.currentTarget as HTMLFormElement;

    const formValid = form.checkValidity();

    if (registering) {
      const emailTaken = await checkEmailExists(email);
      if (emailTaken) {
        dupeEmailError = "This email is already in use";
      }
    }

    if (!formValid) {
      password = "";
      confirmPassword = "";
    }

    if (dupeEmailError || !formValid) {
      loading = false;
      return;
    }

    await onsubmit({ email, name: displayName, password, country });
    // in case all fields are valid but submission still fails
    if (!registering) {
      password = "";
      confirmPassword = "";
    }

    // allows us to pass down the error to email easily without second variable
    if (loginError) {
      dupeEmailError = loginError;
    }
    loading = false;
  }

  $effect(() => {
    if (matchingPasswords) {
      if (confirmPassword && password !== confirmPassword) {
        matchingPasswords.setCustomValidity("Passwords do not match");
      } else {
        matchingPasswords.setCustomValidity("");
      }
    }
  });
</script>

<div class="card">
  <a class="cancel-button" href={resolve(`/`)}> Cancel </a>
  <h2 class="mb-4">
    {registering ? "Register" : "Login"}
  </h2>
  <form class="flex flex-col gap-4" onsubmit={handleSubmit} novalidate>
    <EmailField bind:email {loading} bind:dupeEmailError {submitted} />

    {#if registering}
      <NameField {loading} {submitted} bind:name={displayName} />
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
      <CountryField {loading} {submitted} bind:country />
      <p class="text-xs text-text-muted">
        By registering, you agree to Grinder's
        <a class="redirect-button" href={resolve("/terms")} target="_blank"
          >Terms of Service</a
        >,
        <a class="redirect-button" href={resolve("/privacy")} target="_blank"
          >Privacy Policy</a
        >, and
        <a class="redirect-button" href={resolve("/cookies")} target="_blank"
          >Cookie Policy</a
        >.
      </p>
    {:else}
      <PasswordField
        {submitted}
        {loading}
        bind:password
        errorMessage={loginError || "Invalid Email or Password"}
      />
      <div>
        <button
          type="button"
          class="redirect-button"
          onclick={() => goto(resolve("/reset-password"))}
        >
          Forgot password?
        </button>
      </div>
    {/if}
    <button type="submit" class="primary-button" disabled={loading}>
      {registering ? "Register" : "Login"}
    </button>
    <div>
      {registering ? "Already registered?" : "Haven't registered yet?"}
      <button
        type="button"
        class="redirect-button"
        onclick={() => goto(resolve(registering ? "/login" : "/register"))}
      >
        {registering ? "Login" : "Register"}
      </button>
    </div>
  </form>
</div>
