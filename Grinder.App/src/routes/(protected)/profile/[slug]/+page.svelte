<script lang="ts">
  import ProfileIcon from "$lib/components/svgs/profileicon.svelte";
  import FollowButton from "$lib/components/Friends/FollowButton.svelte";
  import { type PageData } from "./$types";
  import { RelationshipStatus, type Task } from "$lib/types";
  import { user } from "$lib/stores/auth";
  import { goto, invalidateAll } from "$app/navigation";
  import { resolve } from "$app/paths";
  import { page } from "$app/state";
  import TaskShowcase from "$lib/components/tasks/TaskShowcase.svelte";
  import { fetchTasks, flattenTasks } from "$lib/api/tasks";
  import { untrack } from "svelte";
  import TaskContainer from "$lib/components/tasks/TaskContainer.svelte";
  import Grid from "$lib/components/svgs/grid.svelte";
  import List from "$lib/components/svgs/list.svelte";
  import CalendarIcon from "$lib/components/svgs/calendarsvg.svelte";
  import Calendar from "$lib/components/Calendar.svelte";
  import Pencil from "$lib/components/svgs/pencil.svelte";
  import Badge from "$lib/components/svgs/badge.svelte";
  import BadgeIcon from "$lib/components/svgs/badges/BadgeIcon.svelte";
  import BadgeSelectDropdown from "$lib/components/badges/BadgeSelectDropdown.svelte";
  import MessageIcon from "$lib/components/svgs/message.svelte";
  import { tooltip } from "$lib/utils/tooltip";
  import Gift from "$lib/components/svgs/gift.svelte";
  import WrappedModal from "$lib/components/Wrapped/WrappedModal.svelte";
  import {
    fetchWrappedConstantEligibility,
    fetchWrappedStatus,
    dismissWrapped,
  } from "$lib/api/wrapped";

  let { data }: { data: PageData } = $props();

  let relationshipStatus = $derived(
    (data?.profile?.relationshipStatus as RelationshipStatus) ?? null,
  );

  let tasks = $state<Task[]>([]);
  let error = $state("");
  let loading = $state(false);

  let currentYearEligible = $state(false);
  let currentYear = $state<number | null>(null);
  let previousYearEligible = $state(false);
  let previousYear = $state<number | null>(null);
  let wrappedModalYear = $state<number | null>(null);

  let feedViewElement = $state<HTMLDivElement>();

  let flatTasks = $derived(flattenTasks(tasks));
  type View = "feed" | "grid" | "calendar";
  const VIEWS: View[] = ["feed", "grid", "calendar"];

  let requestedView = $derived.by(() => {
    const param = page.url.searchParams.get("view");
    return VIEWS.includes(param as View) ? (param as View) : "feed";
  });
  let view = $derived(
    requestedView === "grid" && $user?.id !== data.profile.id
      ? "feed"
      : requestedView,
  );

  let feedLabel = $derived(
    $user?.id === data.profile.id
      ? "My Feed"
      : `${data.profile.displayName}'s Feed`,
  );

  function setView(next: View) {
    const url = new URL(page.url);
    url.searchParams.set("view", next);
    goto(url, { replaceState: false, noScroll: true, keepFocus: true });
  }

  function handleTaskUpdate(updatedTask: Task) {
    tasks = updateTaskInTree(tasks, updatedTask);
  }

  function updateTaskInTree(list: Task[], updatedTask: Task): Task[] {
    return list.map((t) => {
      if (t.id === updatedTask.id) {
        return { ...t, ...updatedTask };
      }
      if (t.children?.length) {
        return { ...t, children: updateTaskInTree(t.children, updatedTask) };
      }
      return t;
    });
  }

  function handleTasksReordered(listId: number, items: Task[]) {
    tasks = replaceChildren(tasks, listId, items);
  }

  function replaceChildren(
    list: Task[],
    listId: number,
    items: Task[],
  ): Task[] {
    return list.map((t) => {
      if (t.id === listId) return { ...t, children: items };
      if (t.children?.length)
        return { ...t, children: replaceChildren(t.children, listId, items) };
      return t;
    });
  }

  $effect(() => {
    if (!$user || $user.id !== data.profile.id) {
      untrack(() => {
        currentYearEligible = false;
        previousYearEligible = false;
      });
      return;
    }
    fetchWrappedConstantEligibility()
      .then((status) =>
        untrack(() => {
          if (!status) return;
          currentYearEligible = status.currentYearEligible;
          currentYear = status.currentYear;
          previousYearEligible = status.previousYearEligible;
          previousYear = status.previousYear;
        }),
      )
      .catch(() =>
        untrack(() => {
          currentYearEligible = false;
          previousYearEligible = false;
        }),
      );
  });

  function openWrapped(year: number) {
    wrappedModalYear = year;
  }

  $effect(() => {
    if (!$user) return;
    const targetId = data.profile.id !== $user.id ? data.profile.id : undefined;

    untrack(() => {
      loading = true;
    });

    fetchTasks(targetId)
      .then((t) =>
        untrack(() => {
          tasks = t.reverse();
        }),
      )
      .catch((e) =>
        untrack(() => {
          error = e.message;
        }),
      )
      .finally(() =>
        untrack(() => {
          loading = false;
        }),
      );
  });
</script>

<div class="min-h-screen bg-surface-alt p-2 md:p-8">
  <div class="flex flex-col gap-4 w-full max-w-full">
    <!-- Top banner -->
    <div
      class="flex flex-col sm:flex-row sm:justify-between items-center gap-4 m-2"
    >
      <div class="flex flex-col sm:flex-row gap-4 items-center">
        <ProfileIcon
          size={10}
          userId={data.profile.id}
          clickable={false}
          tooltip={true}
        />
        <div class="flex flex-col">
          <div class="flex items-center gap-2">
            <h1>
              {data.profile.displayName}
            </h1>
            {#if $user?.id === data.profile.id}
              <BadgeSelectDropdown
                selectedBadgeName={data.profile.selectedBadgeName}
                selectedBadgeIcon={data.profile.selectedBadgeIcon}
                size={1.5}
                onSelect={() => invalidateAll()}
              />
            {:else if data.profile.selectedBadgeIcon}
              <span use:tooltip={data.profile.selectedBadgeName}>
                <BadgeIcon icon={data.profile.selectedBadgeIcon} size={1} />
              </span>
            {/if}
          </div>
          <div class="inline-flex items-center gap-4">
            <button
              onclick={() =>
                feedViewElement?.scrollIntoView({ behavior: "smooth" })}
              >{tasks.length} <span class="text-text-muted">tasks</span></button
            >
            <a
              href={resolve(`/friends/${data.profile.id}`)}
              class="text-text hover:underline text-center sm:text-left"
            >
              {data.profile.friendCount}
              <span class="text-text-muted"
                >{data.profile.friendCount === 1 ? "friend" : "friends"}</span
              >
            </a>
          </div>
        </div>
      </div>

      {#if $user?.id === data.profile.id}
        <div class="relative flex flex-wrap gap-2">
          <a
            class="icon-button"
            aria-label="Profile settings"
            href={resolve("/profile")}
            use:tooltip={"Profile Settings"}
          >
            <Pencil size={1.5} />
          </a>
          <a
            class="icon-button"
            aria-label="View badges"
            href={resolve(`/badges`)}
            use:tooltip={"View Badges"}
          >
            <Badge size={1.5} />
          </a>
        </div>
      {:else}
        <div class="flex flex-wrap gap-2 items-center">
          {#if relationshipStatus === RelationshipStatus.Friends}
            <a
              class="secondary-button inline-flex items-center gap-2 bg-surface-alt-hover"
              aria-label="Message"
              href={resolve(`/chat/${data.profile.id}`)}
              use:tooltip={"Message"}
            >
              <MessageIcon size={1} />
              <span>Message</span>
            </a>
          {/if}
          <FollowButton userId={data.profile.id} bind:relationshipStatus />
        </div>
      {/if}
    </div>

    <hr class="text-surface-alt-hover" />

    <!-- Switcher -->
    {#if $user?.id === data.profile.id}
      <div class="inline-flex justify-center gap-4 items">
        <button
          class="w-full sm:w-auto justify-center inline-flex gap-2 items-center secondary-button {view ===
          'feed'
            ? 'bg-brand text-surface '
            : ''}"
          data-testid="feed-view-button"
          onclick={() => setView("feed")}
        >
          <Grid size={1.5} />
          <div class="hidden xs:block">{feedLabel}</div>
        </button>

        <button
          class="w-full justify-center sm:w-auto inline-flex gap-2 items-center secondary-button {view ===
          'grid'
            ? 'bg-brand text-surface '
            : ''}"
          data-testid="tasks-view-button"
          onclick={() => setView("grid")}
        >
          <List size={1.5} />
          <div class="hidden xs:block">My Tasks</div>
        </button>

        <button
          class="w-full justify-center sm:w-auto inline-flex gap-2 items-center secondary-button {view ===
          'calendar'
            ? 'bg-brand text-surface '
            : ''}"
          data-testid="calendar-view-button"
          onclick={() => setView("calendar")}
        >
          <CalendarIcon size={1.5} />
          <div class="hidden xs:block">Calendar</div>
        </button>
      </div>
    {/if}

    <div class="relative">
      <div
        class="transition-opacity duration-150 {view === 'feed'
          ? 'relative'
          : 'absolute inset-x-0 top-0 opacity-0 h-0 overflow-hidden'}"
        inert={view !== "feed"}
        data-testid="feed-view-pane"
        bind:this={feedViewElement}
      >
        <TaskShowcase {tasks} isOwnProfile={$user?.id === data.profile.id} />
      </div>
      <div
        class="transition-opacity duration-150 {view === 'grid'
          ? 'relative'
          : 'absolute inset-x-0 top-0 opacity-0 h-0 overflow-hidden'}"
        inert={view !== "grid"}
        data-testid="tasks-view-pane"
      >
        <TaskContainer
          {tasks}
          onTaskUpdate={handleTaskUpdate}
          onTasksReordered={handleTasksReordered}
        />
      </div>
    </div>
  </div>

  {#if view === "calendar"}
    <div class="w-full mt-4" data-testid="calendar-view-pane">
      <Calendar
        tasks={flatTasks}
        onAddTask={() => {}}
        onSelectTask={(taskId: number) => goto(resolve(`/tasks/${taskId}`))}
      />
    </div>
  {/if}
</div>

{#if $user?.id === data.profile.id && (currentYearEligible || previousYearEligible)}
  <div
    class="absolute bottom-16 md:bottom-4 right-4 xs:right-6 md:right-12 xl:right-14 z-20 flex flex-col gap-2 items-end"
  >
    {#if currentYearEligible && currentYear !== null}
      <button
        class="inline-flex gap-2 items-center primary-button"
        aria-label={`View ${currentYear} Wrapped`}
        onclick={() => openWrapped(currentYear!)}
        use:tooltip={`View your ${currentYear} Wrapped`}
      >
        <Gift size={1.5} />
        <span class="hidden md:inline">View Grinder Wrapped</span>
      </button>
    {/if}
    {#if previousYearEligible && previousYear !== null}
      <button
        class="inline-flex gap-2 items-center secondary-button"
        aria-label={`View ${previousYear} Wrapped`}
        onclick={() => openWrapped(previousYear!)}
        use:tooltip={`View your ${previousYear} Wrapped`}
      >
        <Gift size={1.5} />
        <span class="hidden md:inline">View {previousYear} Wrapped</span>
      </button>
    {/if}
  </div>
{/if}

{#if wrappedModalYear !== null}
  <WrappedModal
    open={wrappedModalYear !== null}
    year={wrappedModalYear}
    onclose={() => (wrappedModalYear = null)}
  />
{/if}
