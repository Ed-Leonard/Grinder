<script lang="ts">
  import { resolve } from "$app/paths";
  import EmailField from "$lib/components/FormFields/EmailField.svelte";
  import { checkAuth, user } from "$lib/stores/auth";
  import CountryField from "$lib/components/FormFields/CountryField.svelte";
  import NameField from "$lib/components/FormFields/NameField.svelte";
  import ErrorPopup from "$lib/components/utils/Error.svelte";
  import Success from "$lib/components/utils/Success.svelte";
  import checkEmailExists from "$lib/api/exisitngEmail";
  import ProfilePicture from "$lib/components/media/ProfilePicture.svelte";
  import ExitIcon from "$lib/components/svgs/exit.svelte";
  import { logout } from "$lib/stores/auth";
  import { page } from "$app/state";
  import { generateCode } from "$lib/api/generateCode";
  import { goto } from "$app/navigation";
  import BasePage from "$lib/components/utils/BasePage.svelte";
  import { profilePictureUrl } from "$lib/api/media";
  import { bumpProfilePictureVersion } from "$lib/stores/profilePicture";
  import { updateProfile, uploadProfilePicture } from "$lib/api/profile";

  let email = $state("");
  let displayName = $state("");
  let country = $state("");
  let loading = $state(false);
  let dupeEmailError = $state("");
  let error = $state("");
  let submitted = $state(false);
  let pendingFile = $state<File | null>(null);
  let pendingPreviewUrl = $state<string | null>(null);

  let previousEmail = "";
  let hydrated = false;

  let success = $state(page.state?.success ?? "");
  $effect(() => {
    if ($user && !hydrated) {
      email = $user.email;
      displayName = $user.displayName;
      country = $user.country;
      previousEmail = $user.email;
      hydrated = true;
    }
  });

  async function handleUpdatePassword() {
    const err = await generateCode(1, $user?.email);
    goto(resolve("/update-password"), { state: { error: err ?? "" } });
  }

  function revokePendingPreviewUrl() {
    if (pendingPreviewUrl) {
      try {
        URL.revokeObjectURL(pendingPreviewUrl);
      } catch {}
      pendingPreviewUrl = null;
    }
  }

  /**
   * Handles the form submission for editing the user's profile.
   */
  async function handleSubmit(e: SubmitEvent) {
    e.preventDefault();

    submitted = true;
    dupeEmailError = "";

    const form = e.currentTarget as HTMLFormElement;

    if ($user?.email !== email) {
      let emailTaken = false;
      if (previousEmail !== email) {
        emailTaken = await checkEmailExists(email);
      }
      previousEmail = email;
      if (emailTaken) {
        dupeEmailError =
          "This email address is already in use by another account";
      }
    }

    const formValid = form.checkValidity();

    if (dupeEmailError || !formValid) return;

    try {
      loading = true;
      dupeEmailError = "";
      error = "";

      const response = await updateProfile({ email, displayName, country });

      if (response.status === 409) {
        dupeEmailError =
          "This email address is already in use by another account";
        return;
      }
      if (!response.ok) {
        throw new Error("Failed to update profile");
      }

      if (pendingFile) {
        try {
          const uploadResponse = await uploadProfilePicture(pendingFile);
          if (!uploadResponse.ok) {
            const text = await uploadResponse.text();
            throw new Error(text || "Failed to upload profile picture");
          }
          bumpProfilePictureVersion();
        } finally {
          pendingFile = null;
          revokePendingPreviewUrl();
        }
      }

      await checkAuth();
      if ($user === null) goto(resolve("/login" as any));
      submitted = false;

      goto(resolve(`/profile/${$user!.id}`), {
        state: { success: "Profile edited successfully." },
      });
    } catch (err) {
      console.error(err);
      error = "Failed to update profile";
    } finally {
      loading = false;
    }
  }
</script>

<BasePage maxWidth="max-w-xl" class="bg-surface! md:bg-surface-alt! ">
  {#if success}
    <Success {success} />
  {/if}
  <div class="card md:pt-6">
    {#if $user}
      <div class="flex items-center justify-center relative mb-6">
        <div class="absolute top-0 right-0">
          <button
            type="button"
            class="secondary-button flex items-center gap-2 text-red-500"
            onclick={logout}
          >
            <ExitIcon size={1.5} />
            Logout
          </button>
        </div>
        <div class="flex flex-col items-center pt-6 gap-3">
          <ProfilePicture
            profilePicture={profilePictureUrl($user.id)}
            overridePreviewUrl={pendingPreviewUrl}
            onRefresh={checkAuth}
            editable={true}
            onConfirm={(detail: { file: File }) => {
              const f = detail?.file ?? null;
              if (!f) return;
              if (pendingPreviewUrl) {
                revokePendingPreviewUrl();
              }
              pendingFile = f;
              try {
                pendingPreviewUrl = URL.createObjectURL(f);
              } catch {
                pendingPreviewUrl = null;
              }
            }}
          />
          <h1 class="wrap-break-word max-w-sm">Edit Profile</h1>
        </div>
      </div>
      <div class="profile-card px-4">
        {#if error}
          <ErrorPopup {error} />
        {/if}
        <form class="flex flex-col gap-4" novalidate onsubmit={handleSubmit}>
          <NameField bind:name={displayName} {loading} {submitted} />
          <EmailField bind:email {dupeEmailError} {loading} {submitted} />
          <CountryField bind:country {loading} {submitted} />
          <button
            class="primary-button mt-2"
            type="button"
            onclick={handleUpdatePassword}
          >
            Update Password
          </button>
          <div class="inline-flex items-center gap-4">
            <a
              type="button"
              class="secondary-button"
              href={resolve(`/profile/${$user.id}`)}
              onclick={revokePendingPreviewUrl}>Cancel</a
            >
            <button type="submit" class="primary-button w-full">Update</button>
          </div>
        </form>
        <div
          class="mt-6 flex flex-wrap items-center justify-center gap-x-4 gap-y-1 border-t border-surface-alt pt-4 text-xs text-text-muted"
        >
          <a class="underline hover:text-text" href={resolve("/terms")}
            >Terms of Service</a
          >
          <a class="underline hover:text-text" href={resolve("/privacy")}
            >Privacy Policy</a
          >
          <a class="underline hover:text-text" href={resolve("/cookies")}
            >Cookie Policy</a
          >
          <a
            class="underline hover:text-text"
            href={resolve("/attributions")}>Attributions</a
          >
        </div>
      </div>
    {/if}
  </div>
</BasePage>
