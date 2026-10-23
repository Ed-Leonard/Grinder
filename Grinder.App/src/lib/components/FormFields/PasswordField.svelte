<script lang="ts">
  import Eye from "../svgs/eye.svelte";
  import { tooltip } from "$lib/utils/tooltip";

  let {
    id = "password",
    label = "Password",
    password = $bindable(""),
    submitted = false,
    loading = false,
    pattern,
    errorMessage = "",
    ref = $bindable(null),
  }: {
    id?: string;
    label?: string;
    password: string;
    submitted?: boolean;
    loading?: boolean;
    pattern?: string;
    errorMessage?: string;
    ref?: HTMLInputElement | null;
  } = $props();

  let showPassword = $state(false);
</script>

<div class="flex items-start gap-2">
  <div class="floating relative flex-1">
    <input
      type={showPassword ? "text" : "password"}
      {id}
      class="input-field peer {submitted ? 'submitted' : ''}"
      placeholder={label}
      bind:value={password}
      disabled={loading}
      required
      {pattern}
      bind:this={ref}
    />
    <label for={id} class="input-label">{label}</label>
    <p class="input-invalid">
      {errorMessage}
    </p>
  </div>
  <button
    class="icon-button self-center"
    type="button"
    onclick={() => (showPassword = !showPassword)}
    tabindex="-1"
    aria-label={showPassword ? "Hide password" : "Show password"}
    use:tooltip={showPassword ? "Hide password" : "Show password"}
  >
    <Eye show={showPassword} size={1.25} />
  </button>
</div>
