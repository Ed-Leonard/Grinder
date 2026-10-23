<script lang="ts">
  let { loading = false, submitted = false, name = $bindable("") } = $props();
  let inputEl: HTMLInputElement;

  function validateDisplayName(value: string): string {
    value = value.trim();
    if (value.length < 3 || value.length > 64) {
      return "Display name must be between 3 and 64 characters";
    }
    if (/[^\p{L}\s\-']/u.test(value)) {
      return "Display name must only include letters, spaces, hyphens or apostrophes";
    }
    return "valid";
  }

  function validate(value: string) {
    const result = validateDisplayName(value);
    inputEl?.setCustomValidity(result === "valid" ? "" : result);
  }
</script>

<div class="floating relative">
  <input
    bind:this={inputEl}
    type="text"
    class="input-field peer {submitted ? 'submitted' : ''}"
    id="floatingName"
    placeholder="Display Name"
    bind:value={name}
    disabled={loading}
    required
    oninput={() => validate(name)}
    onblur={() => {
      name = name.trim();
      validate(name);
    }}
  />
  <label for="floatingName" class="input-label"
    >Display Name (3-64 Characters) {name.length > 0
      ? name.length + "/64"
      : ""}</label
  >
  <p class="input-invalid">
    {validateDisplayName(name)}
  </p>
</div>
