<script lang="ts">
  let {
    size = 1,
    mirrored = false,
  }: {
    size?: number;
    mirrored?: boolean;
  } = $props();
</script>

<div style="width: {size}rem; height: {size}rem;">
  <svg
    style={mirrored ? "" : "transform: scaleX(-1)"}
    fill="currentColor"
    viewBox="0 0 337.792 337.792"
    xmlns="http://www.w3.org/2000/svg"
  >
    <path
      d="M337.792,134.824l-130.824,99.441v-52.216C117.061,173.419,30.735,207.817,0.001,302.41 C-0.337,180.728,99.895,111.781,206.968,88.488V35.382L337.792,134.824z"
    ></path></svg
  >
</div>
