<script lang="ts">
  import UndoSvg from "$lib/components/svgs/undo.svelte";
  import { tooltip } from "$lib/utils/tooltip";

  export let handleUndo: () => void;
  export let handleRedo: () => void;
</script>

<div class="inline-flex">
  <button
    class="icon-button"
    use:tooltip={"Undo Change"}
    onclick={() => handleUndo()}><UndoSvg size={1.5} /></button
  >
  <button
    class="icon-button"
    use:tooltip={"Redo Change"}
    onclick={() => handleRedo()}><UndoSvg mirrored={true} size={1.5} /></button
  >
</div>
