<script lang="ts">
  import {
    toggleTaskReaction,
    type Reaction,
    type ReactionResponse,
  } from "$lib/api/reaction";
  import ReactionPickerButton from "./ReactionPickerButton.svelte";
  import ReactionList from "./ReactionList.svelte";
  import { untrack } from "svelte";
  import { resolve } from "$app/paths";

  let {
    taskId,
    initialReactions = [],
    showReactions = true,
    active = false,
    class: className = "",
  }: {
    taskId: number;
    initialReactions?: ReactionResponse[];
    showReactions?: boolean;
    active?: boolean;
    class?: string;
  } = $props();

  let reactions = $derived<ReactionResponse[]>(initialReactions);
  const selectedNames = $derived(
    reactions.filter((r) => r.reactedByCurrentUser).map((r) => r.reaction.name),
  );

  type Floater = {
    id: number;
    fileName: string;
    startX: number;
    endX: number;
    duration: number;
    rotation: number;
  };

  let floaters = $state<Floater[]>([]);
  let floaterId = 0;
  const MAX_FLOATERS = 30;
  const MAX_PER_REACTION_ON_LOAD = 3;

  function spawnFloater(fileName: string) {
    const id = floaterId++;
    const startX = Math.random() * 30 - 15;
    const endX = startX + (Math.random() * 60 - 30);
    const duration = 1.6 + Math.random() * 0.8;
    const rotation = Math.random() * 30 - 15;

    const next = [
      ...floaters,
      { id, fileName, startX, endX, duration, rotation },
    ];
    floaters =
      next.length > MAX_FLOATERS
        ? next.slice(next.length - MAX_FLOATERS)
        : next;

    setTimeout(() => {
      floaters = floaters.filter((f) => f.id !== id);
    }, duration * 1000);
  }

  function burstExistingReactions() {
    let delay = 0;
    for (const r of reactions ?? []) {
      const spawnCount = Math.min(r.count, MAX_PER_REACTION_ON_LOAD);
      for (let i = 0; i < spawnCount; i++) {
        delay += 120 + Math.random() * 180;
        setTimeout(() => spawnFloater(r.reaction.fileName), delay);
      }
    }
  }

  $effect(() => {
    if (active) {
      untrack(() => {
        for (const r of reactions) {
          const img = new Image();
          img.src = `/reactions/${r.reaction.fileName}`;
        }
        burstExistingReactions();
      });
    }
  });

  async function handleReaction(reaction: Reaction) {
    const existing = reactions.find((r) => r.reaction.id === reaction.id);
    const isAdding = !existing || !existing.reactedByCurrentUser;
    const snapshot = reactions;

    reactions = existing
      ? existing.reactedByCurrentUser
        ? reactions
            .map((r) =>
              r.reaction.id === reaction.id
                ? { ...r, count: r.count - 1, reactedByCurrentUser: false }
                : r,
            )
            .filter((r) => r.count > 0)
        : reactions.map((r) =>
            r.reaction.id === reaction.id
              ? { ...r, count: r.count + 1, reactedByCurrentUser: true }
              : r,
          )
      : [...reactions, { reaction, count: 1, reactedByCurrentUser: true }];

    if (isAdding) spawnFloater(reaction.fileName);

    try {
      const updated = await toggleTaskReaction(taskId, reaction.id);
      reactions = reactions
        .map((r) => (r.reaction.id === updated.reaction.id ? updated : r))
        .filter((r) => r.count > 0);
    } catch (error) {
      console.error("Failed to toggle reaction:", error);
      reactions = snapshot;
    }
  }
</script>

<ReactionPickerButton
  {selectedNames}
  onPick={handleReaction}
  class={className}
  {showReactions}
/>
{#if showReactions}
  <ReactionList {reactions} onToggle={handleReaction} />
{:else}
  <div class="relative items-center flex flex-col">
    <div class="text-sm text-white">
      {reactions.reduce((sum, r) => sum + r.count, 0)}
    </div>
    <div class="pointer-events-none absolute inset-0">
      {#each floaters as f (f.id)}
        <span
          class="floater absolute bottom-16 left-0"
          style="--startX: {f.startX}px; --endX: {f.endX}px; --dur: {f.duration}s; --rot: {f.rotation}deg;"
        >
          <img
            src={resolve(`/reactions/${f.fileName}` as any)}
            alt={f.fileName}
            loading="eager"
            decoding="async"
            class="pointer-events-none h-6 w-6 object-contain"
          />
        </span>
      {/each}
    </div>
  </div>
{/if}

<style>
  .floater {
    animation: float-up var(--dur) ease-out forwards;
    will-change: transform, opacity;
  }

  @keyframes float-up {
    0% {
      transform: translate(var(--startX), 0) scale(0.6) rotate(0deg);
      opacity: 0;
    }
    10% {
      opacity: 1;
    }
    100% {
      transform: translate(var(--endX), -140px) scale(1.1) rotate(var(--rot));
      opacity: 0;
    }
  }
</style>
