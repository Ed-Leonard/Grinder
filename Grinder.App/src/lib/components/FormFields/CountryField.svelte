<script lang="ts">
  import { countryFlag, countries } from "$lib/countries";

  let {
    loading = false,
    submitted = false,
    country = $bindable(""),
  } = $props();
</script>

<div class="floating relative">
  <select
    class="input-field peer cursor-pointer appearance-none pr-10 {submitted
      ? 'submitted'
      : ''}"
    id="countrySelect"
    placeholder="Country"
    disabled={loading}
    required
    bind:value={country}
  >
    <option selected value={""}>Select a Country</option>
    {#each countries as country}
      <option value={country.name}
        >{countryFlag(country.code)} {country.name}</option
      >
    {/each}
  </select>
  <label for="countrySelect" class="input-label"> Country </label>
  <svg
    class="pointer-events-none absolute right-4 top-[50%] -translate-y-1/2 text-text-muted"
    xmlns="http://www.w3.org/2000/svg"
    width="18"
    height="18"
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    stroke-width="2"
    stroke-linecap="round"
    stroke-linejoin="round"
  >
    <path d="m6 9 6 6 6-6" />
  </svg>
  <p class="input-invalid">Please select an option</p>
</div>
