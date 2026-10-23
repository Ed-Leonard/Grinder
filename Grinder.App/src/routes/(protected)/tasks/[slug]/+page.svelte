<script lang="ts">
  import { page } from "$app/state";
  import { goto } from "$app/navigation";
  import { resolve } from "$app/paths";
  import TaskDate from "$lib/components/tasks/TaskDate.svelte";
  import AttachmentView from "$lib/components/media/AttachmentView.svelte";
  import {
    colourForStatus,
    statusBadgeClass,
    statusLabel,
  } from "$lib/utils/taskUtils";
  import { type Task } from "$lib/types";
  import type { PageData } from "./$types";
  import TaskIcon from "$lib/components/tasks/TaskIcon.svelte";
  import Pencil from "$lib/components/svgs/pencil.svelte";
  import Infocircle from "$lib/components/svgs/infocircle.svelte";
  import CreateComment from "$lib/components/Comments/CreateComment.svelte";
  import ListComment from "$lib/components/Comments/ListComment.svelte";
  import TaskReactions from "$lib/components/reactions/TaskReactions.svelte";
  import { user } from "$lib/stores/auth";
  import { onMount } from "svelte";
  import { getUserProfile } from "$lib/api/profile";
  import type { User } from "$lib/types";
  import Pluscircle from "$lib/components/svgs/pluscircle.svelte";
  import BasePage from "$lib/components/utils/BasePage.svelte";
  import { tooltip } from "$lib/utils/tooltip";
  import ShareButton from "$lib/components/ShareButton.svelte";

  let { data }: { data: PageData } = $props();
  let hasStartDate = $derived(!!data.task.startDate);
  let isOwner = $derived($user?.id === data.task.userId);
  let listCommentRef: any;
  let commentCount = $state(0);
  let author = $state<User | null>();

  type Breadcrumb = {
    id: number;
    title: string;
  };

  function parseBreadcrumbs(raw: string | null): Breadcrumb[] {
    if (!raw) return [];

    try {
      const parsed = JSON.parse(decodeURIComponent(raw));
      if (!Array.isArray(parsed)) return [];

      const crumbs: Breadcrumb[] = [];
      for (const crumb of parsed) {
        if (
          typeof crumb === "object" &&
          crumb !== null &&
          typeof crumb.id === "number" &&
          Number.isFinite(crumb.id) &&
          typeof crumb.title === "string"
        ) {
          const duplicateIndex = crumbs.findIndex((c) => c.id === crumb.id);
          if (duplicateIndex >= 0) {
            crumbs.splice(duplicateIndex + 1);
          } else {
            crumbs.push({ id: crumb.id, title: crumb.title });
          }
        }
      }

      return crumbs;
    } catch {
      return [];
    }
  }

  const breadcrumbs = $derived.by(() => {
    const fromQuery = parseBreadcrumbs(page.url.searchParams.get("crumbs"));

    // Fallback: no crumbs param means we arrived directly (e.g. from the
    // dashboard), so build the trail from the task's own ancestor chain.
    const parsed = fromQuery.length > 0 ? fromQuery : (data.ancestors ?? []);

    const currentInPath = parsed.findIndex(
      (crumb) => crumb.id === data.task.id,
    );
    const priorPath =
      currentInPath >= 0 ? parsed.slice(0, currentInPath) : parsed;
    return [...priorPath, { id: data.task.id, title: data.task.title }];
  });

  const sortedChildren = $derived.by(() =>
    [...(data.task.children ?? [])].sort(
      (a: Task, b: Task) => a.order - b.order,
    ),
  );

  const mediaAttachments = $derived(
    (data.task.taskAttachments ?? []).filter(
      (attachment) =>
        attachment.contentType.startsWith("image/") ||
        attachment.contentType.startsWith("video/"),
    ),
  );

  function taskUrl(taskId: number, crumbPath: Breadcrumb[]): string {
    if (crumbPath.length === 0) return resolve(`/tasks/${taskId}`);
    return `${resolve(`/tasks/${taskId}`)}?crumbs=${encodeURIComponent(JSON.stringify(crumbPath))}`;
  }

  function navigateToChild(child: Task): void {
    goto(taskUrl(child.id, breadcrumbs));
  }

  function navigateToBreadcrumb(index: number): void {
    const target = breadcrumbs[index];
    if (!target) return;
    goto(taskUrl(target.id, breadcrumbs.slice(0, index)));
  }

  onMount(async () => {
    if (isOwner) return;
    const response = await getUserProfile(data.task.userId, fetch);

    if (!response.ok) {
      return;
    }

    author = await response.json();
  });
</script>

<BasePage
  maxWidth="max-w-7xl md:space-y-4 md:m-4"
  class="bg-surface! md:bg-surface-alt!"
>
  <section class="card">
    <div class="flex flex-col items-start justify-between gap-2">
      <div class="min-w-0 flex flex-wrap justify-between w-full">
        <nav
          aria-label="Task breadcrumb"
          class="flex min-w-0 flex-1 flex-wrap items-center gap-1 text-sm text-text-muted"
        >
          <span class="flex shrink-0 items-center gap-1">
            <a
              class="text-text-muted hover:text-text hover:underline"
              href={resolve(`/profile/${data.task.userId}`)}
            >
              {!isOwner
                ? (author?.displayName ?? "Tasks") + "'s Tasks"
                : "Tasks"}
            </a>
            {#if breadcrumbs.length > 0}
              <span aria-hidden="true">/</span>
            {/if}
          </span>

          {#each breadcrumbs as crumb, index (crumb.id)}
            <span class="flex min-w-0 items-center gap-1">
              {#if index === breadcrumbs.length - 1}
                <span class="max-w-2xl truncate font-medium text-text"
                  >{crumb.title}</span
                >
              {:else}
                <button
                  type="button"
                  class="max-w-2xl truncate cursor-pointer text-text-muted hover:text-text hover:underline"
                  onclick={() => navigateToBreadcrumb(index)}
                >
                  {crumb.title}
                </button>
                <span aria-hidden="true">/</span>
              {/if}
            </span>
          {/each}
        </nav>
        {#if isOwner}
          <a
            class="icon-button"
            use:tooltip={"Edit task"}
            aria-label="Edit task"
            href={resolve(`/tasks/${data.task.id}/edit`)}
          >
            <Pencil size={1.25} />
          </a>
        {/if}
      </div>

      <div class="flex flex-wrap items-start justify-between w-full">
        <div class="flex min-w-0 flex-1 flex-row items-end">
          <h1 class="wrap-anywhere text-left">
            {data.task.title}
          </h1>
        </div>

        <span
          class="inline-flex items-center gap-2 rounded-full px-4 py-2 text-sm font-medium text-white {statusBadgeClass(
            data.task.itemStatus,
          )}"
        >
          <TaskIcon status={data.task.itemStatus} colour="white" />
          {statusLabel(data.task.itemStatus)}
        </span>
      </div>
    </div>
    <div
      class="mt-6 grid gap-4
    [grid-template-areas:'media'_'description'_'subtasks'_'reactions']
    md:grid-cols-[65fr_35fr] md:items-start
	md:[grid-template-areas:'media_description'_'subtasks_description'_'reactions_reactions']"
    >
      <div>
        {#if mediaAttachments.length > 0}
          <AttachmentView attachments={mediaAttachments} />
        {:else}
          <div
            class="flex h-full min-h-32 items-center justify-center rounded-lg border border-dashed border-surface-alt-hover bg-surface-alt p-4 text-sm text-text-muted"
          >
            No media attached.
          </div>
        {/if}
      </div>

      <div
        class="rounded-lg border border-surface-alt bg-surface p-4 [grid-area:subtasks]"
      >
        {#if data.task.depth >= 2}
          <div
            class="flex items-center gap-2 rounded-lg text-sm text-text-muted"
          >
            <Infocircle />
            <span
              >This task is nested at the maximum depth. It cannot have subtasks
              of its own.</span
            >
          </div>
        {:else}
          <div class="mb-3 flex shrink-0 items-center justify-between gap-2">
            <h3 class="text-left">Subtasks</h3>
            {#if isOwner}
              <button
                class="primary-button inline-flex gap-2 items-center"
                onclick={() =>
                  goto(resolve(`/tasks/new?parent=${data.task.id}`))}
              >
                <Pluscircle />
                Add subtask
              </button>
            {/if}
          </div>
          {#if sortedChildren.length > 0}
            <div
              class="flex flex-col min-h-0 flex-1 gap-2 overflow-y-auto pr-1"
            >
              {#each sortedChildren as child (child.id)}
                <button
                  type="button"
                  class="min-w-0 truncate w-full rounded-lg border border-surface-alt p-2 text-left transition-colors hover:bg-surface-hover"
                  style="border-left: 4px solid {colourForStatus(
                    child.itemStatus,
                  )}"
                  onclick={() => navigateToChild(child)}
                >
                  <div class="flex items-start justify-between gap-2">
                    <h5 class="min-w-0 truncate">
                      {child.title}
                    </h5>
                    <span
                      class="shrink-0 rounded-full py-1.5 px-3 text-xs font-medium inline-flex gap-2 text-white {statusBadgeClass(
                        child.itemStatus,
                      )}"
                    >
                      <TaskIcon status={child.itemStatus} colour="white" />
                      {statusLabel(child.itemStatus)}
                    </span>
                  </div>
                  <p class="min-w-0 truncate text-xs text-text-muted">
                    {child.description && child.description.trim()
                      ? child.description
                      : "No description"}
                  </p>
                  <div class="mt-2 text-xs text-text-muted">
                    <TaskDate
                      date={child.dueDate}
                      time={child.dueTime}
                      className="text-xs text-text-muted"
                    />
                  </div>
                </button>
              {/each}
            </div>
          {:else}
            <p
              class="rounded-lg border border-dashed border-surface-alt-hover bg-surface-alt p-4 text-sm text-text-muted"
            >
              No subtasks yet.
            </p>
          {/if}
        {/if}
      </div>

      <div
        class="flex min-h-0 max-h-[80vh] overflow-y-auto flex-col rounded-lg border border-surface-alt p-4 [grid-area:description]"
      >
        <div class="mb-3 flex flex-wrap items-center justify-between">
          <h3 class="text-left">Description</h3>
          <div class="py-2">
            {#if hasStartDate}
              <p class="text-sm">
                <TaskDate
                  date={data.task.startDate ?? ""}
                  time={data.task.startTime ?? ""}
                />

                -

                <TaskDate
                  date={data.task.dueDate}
                  time={data.task.dueTime ?? ""}
                />
              </p>
            {:else}
              <p class="text-sm">
                <TaskDate
                  date={data.task.dueDate}
                  time={data.task.dueTime ?? ""}
                />
              </p>
            {/if}
          </div>
        </div>

        <p
          class="mt-2 whitespace-pre-wrap wrap-anywhere text-sm text-text-muted"
        >
          {data.task.description?.trim() || "No description"}
        </p>

        {#if data.task.categories && data.task.categories.length > 0}
          <div class="flex flex-wrap pt-6 items-center gap-2">
            {#each data.task.categories as category (category.id)}
              <span
                class="rounded-full bg-surface-alt border border-surface-alt-hover px-3 py-0.5 text-xs font-medium text-text-muted"
              >
                {category.name}
              </span>
            {/each}
          </div>
        {/if}
      </div>
      <div
        class="flex flex-row-reverse flex-wrap items-center gap-2 [grid-area:reactions]"
      >
        <ShareButton taskId={data.task.id} />
        <TaskReactions
          taskId={data.task.id}
          initialReactions={data.task.reactions ?? []}
        />
      </div>
    </div>
  </section>
  <div class="card px-4 max-w-full space-y-4">
    <h2 class="font-semibold text-left">Comments ({commentCount})</h2>
    <CreateComment
      isFeed={false}
      taskId={data.task.id}
      onCommentCreated={(comment) => listCommentRef?.addComment(comment)}
    />
    <div class="overflow-y-auto">
      <ListComment
        bind:this={listCommentRef}
        isFeed={false}
        showHeading={false}
        onCommentCountChange={(count) => (commentCount = count)}
        taskId={data.task.id}
        currentUserId={$user?.id ?? null}
      />
    </div>
  </div>
</BasePage>
