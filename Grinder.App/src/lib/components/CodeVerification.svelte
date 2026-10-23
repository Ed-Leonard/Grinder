<script lang="ts">
  import { fetchWithCsrf } from "$lib/csrf";
  import { resolve } from "$app/paths";
  import { onMount } from "svelte";
  import ErrorPopup from "$lib/components/utils/Error.svelte";

  let {
    email,
    type,
    onSuccess,
    confirmLabel = "Confirm",
  }: {
    email: string;
    type: number;
    onSuccess: () => void;
    confirmLabel?: string;
  } = $props();

  let loading = $state(false);
  let error = $state("");

  let digits = $state(["", "", "", "", "", ""]);
  let inputs = $state<HTMLInputElement[]>([]);

  let resendCooldown = $state(10);
  let resendInterval: ReturnType<typeof setInterval> | null = null;

  onMount(() => {
    startCooldown();
    return () => {
      if (resendInterval) {
        clearInterval(resendInterval);
      }
    };
  });

  async function handleVerify() {
    try {
      loading = true;
      error = "";
      const response = await fetchWithCsrf(
        resolve("/api/verificationcode/verify" as any),
        {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            email,
            code: digits.join(""),
            type,
          }),
        },
      );

      if (response.status == 400) {
        const message = await response.json();
        error = message.message;
        return;
      }
      if (response.status == 404) {
        const message = await response.json();
        error = message.message;
        return;
      }
      if (response.status == 422) {
        const message = await response.json();
        error = message.message;
        return;
      }

      if (!response.ok) {
        error = "An unexpected error occurred.";
        return;
      }

      onSuccess();
    } catch (err) {
      error = "Verification failed: " + (err as Error).message;
    } finally {
      loading = false;
    }
  }

  async function handleResend() {
    try {
      await fetchWithCsrf(resolve("/api/verificationcode/generate" as any), {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, type }),
      });
      startCooldown();
    } catch (err) {
      error = "Failed to resend code: " + (err as Error).message;
    }
  }

  function handleInput(index: number, e: Event) {
    const target = e.target as HTMLInputElement;
    const val = target.value.replace(/\D/g, "").slice(-1);
    digits[index] = val;
    target.value = val;
    if (val && index < 5) inputs[index + 1]?.focus();
  }

  function handleKeydown(index: number, e: KeyboardEvent) {
    if (e.key === "Backspace" && !digits[index] && index > 0) {
      inputs[index - 1]?.focus();
    }
    if (e.key === "Enter" && digits.join("").length === 6 && !loading) {
      handleVerify();
    }
  }

  function startCooldown() {
    resendCooldown = 10;
    resendInterval = setInterval(() => {
      resendCooldown--;
      if (resendCooldown <= 0 && resendInterval) {
        clearInterval(resendInterval);
        resendInterval = null;
      }
    }, 1000);
  }

  function handlePaste(e: ClipboardEvent) {
    e.preventDefault();
    const pasted =
      e.clipboardData?.getData("text").replace(/\D/g, "").slice(0, 6) ?? "";
    pasted.split("").forEach((char, i) => {
      if (i < 6) digits[i] = char;
    });
    inputs[Math.min(pasted.length, 5)]?.focus();
  }
</script>

{#if error}
  <ErrorPopup {error} />
{/if}

<p class="text-center text-sm text-text-muted mb-6">
  Enter the 6-digit code sent to your email.
</p>

<div class="grid grid-cols-6 gap-2 mb-6" onpaste={handlePaste}>
  {#each digits as digit, i}
    <input
      bind:this={inputs[i]}
      type="text"
      inputmode="numeric"
      maxlength="1"
      value={digit}
      oninput={(e) => handleInput(i, e)}
      onkeydown={(e) => handleKeydown(i, e)}
      class="w-full h-12 rounded-lg border text-center text-xl font-semibold transition-colors focus:outline-none focus:border-brand {digit !==
      ''
        ? 'border-primary'
        : 'border-surface-alt'}"
      aria-label="Digit {i + 1}"
    />
  {/each}
</div>

<div class="flex flex-col">
  <button
    type="button"
    class="primary-button"
    disabled={digits.join("").length < 6 || loading}
    onclick={handleVerify}
  >
    {confirmLabel}
  </button>
  <button
    type="button"
    disabled={resendCooldown > 0}
    onclick={handleResend}
    class="mt-3 text-sm text-text-muted underline transition-colors hover:text-text disabled:cursor-not-allowed disabled:text-text-muted/50 disabled:no-underline"
  >
    {resendCooldown > 0 ? `Resend code (${resendCooldown}s)` : "Resend code"}
  </button>
</div>
