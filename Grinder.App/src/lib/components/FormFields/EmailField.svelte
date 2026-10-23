<script lang="ts">
  let emailError = $state("");
  let {
    email = $bindable(""),
    loading = false,
    dupeEmailError = $bindable(""),
    emailValid = $bindable(!emailError),
    submitted = false,
  } = $props();

  let emailInputEl: HTMLInputElement;

  function validateEmail(value: string) {
    var invalidMessage =
      "Invalid email address. Email must be in the format 'jane@doe.nz'";

    if (!value) return invalidMessage;

    const atIndex = value.indexOf("@");
    if (atIndex === -1 || atIndex !== value.lastIndexOf("@"))
      return invalidMessage;

    const local = value.slice(0, atIndex);
    const domain = value.slice(atIndex + 1);

    if (!local || !domain) return invalidMessage;
    if (local.length > 64) return invalidMessage;
    if (domain.length > 255) return invalidMessage;

    // nasty looking regex but basically checks stuff from the handout provided
    if (
      !/^[a-zA-Z0-9!#$%&'*+/=?^_{|}\`\-]+(\.[a-zA-Z0-9!#$%&'*+/=?^_{|}\`\-]+)*$/.test(
        local,
      )
    )
      return invalidMessage;

    // splits domains and handles them independently
    const labels = domain.split(".");
    if (labels.length < 2) return invalidMessage;

    for (const label of labels) {
      if (label.length > 63) return invalidMessage;
      if (!/^[a-zA-Z0-9]([a-zA-Z0-9\-]*[a-zA-Z0-9])?$/.test(label))
        return invalidMessage;
    }

    const tld = labels[labels.length - 1];
    if (/^\d+$/.test(tld)) return invalidMessage;

    return "";
  }

  $effect(() => {
    if (emailInputEl) {
      emailInputEl.setCustomValidity(dupeEmailError || emailError);
    }
  });

  function handleEmailInput(e: any) {
    const value = (e.target as HTMLInputElement).value;
    dupeEmailError = "";
    emailError = validateEmail(value);
    emailValid = !emailError;
    emailInputEl.setCustomValidity(emailError);
  }
</script>

<div class="floating relative">
  <input
    bind:this={emailInputEl}
    type="email"
    class="input-field peer {submitted ? 'submitted' : ''}"
    id="floatingEmail"
    placeholder="jane@doe.nz"
    bind:value={email}
    disabled={loading}
    required
    oninput={handleEmailInput}
    oninvalid={handleEmailInput}
  />
  <label for="floatingEmail" class="input-label">Email address</label>
  <p class="input-invalid">{dupeEmailError || emailError || "l"}</p>
</div>
