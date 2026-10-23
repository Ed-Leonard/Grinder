<script lang="ts">
  import { DayFlowCalendar, useCalendarApp } from "@dayflow/svelte";
  import {
    createDayView,
    createWeekView,
    createMonthView,
    ViewType,
    createEventsPlugin,
    createEvent,
    createAllDayEvent,
    type Event,
  } from "@dayflow/core";
  import type { Task } from "$lib/types";
  import { TaskStatus, colourForStatus, statusLabel } from "$lib/types";
  import TaskIcon from "$lib/components/tasks/TaskIcon.svelte";
  import { onMount } from "svelte";
  import { fetchCategories } from "$lib/api/category";
  import Cross from "./svgs/cross.svelte";
  import { type Category } from "$lib/api/category";
  import { Temporal } from "temporal-polyfill";
  import { theme } from "$lib/stores/theme";
  import Xicon from "./svgs/xicon.svelte";

  // Flakiest code of all time be careful touching any of this

  // Dayflow requires ids in the this kind of form so we cant use the raw enum
  // also allows us to colour them easily but doesn't provide much freedom see app.css for some more styling
  const statusCalendars = [
    {
      id: "Todo",
      name: "To Do",
      colors: {
        lineColor: "var(--color-todo)",
        eventColor: "color-mix(in srgb, var(--color-todo) 15%, transparent)",
        eventSelectedColor:
          "color-mix(in srgb, var(--color-todo) 60%, transparent)",
        textColor: "var(--color-todo)",
      },
    },
    {
      id: "InProgress",
      name: "In Progress",
      colors: {
        lineColor: "var(--color-inprogress)",
        eventColor:
          "color-mix(in srgb, var(--color-inprogress) 15%, transparent)",
        eventSelectedColor:
          "color-mix(in srgb, var(--color-inprogress) 60%, transparent)",
        textColor: "var(--color-inprogress)",
      },
    },
    {
      id: "Done",
      name: "Done",
      colors: {
        lineColor: "var(--color-done)",
        eventColor: "color-mix(in srgb, var(--color-done) 15%, transparent)",
        eventSelectedColor:
          "color-mix(in srgb, var(--color-done) 60%, transparent)",
        textColor: "var(--color-done)",
      },
    },
  ];

  const statusToCalendarId: Record<TaskStatus, string> = {
    [TaskStatus.Todo]: "Todo",
    [TaskStatus.InProgress]: "InProgress",
    [TaskStatus.Done]: "Done",
  };

  let {
    tasks = [],
    onSelectTask,
    onAddTask,
  }: {
    tasks: Task[];
    onSelectTask: (taskId: number) => void;
    onAddTask: (date: Date) => void;
  } = $props();

  let statusFilter = $state<TaskStatus[]>([]);
  let categoryFilter = $state<Category[]>([]);
  let categories = $state<Category[]>([]);
  let categoriesLoading = $state(true);
  let categorySearch = $state("");

  let filteredCategories = $derived(
    categories.filter((category) =>
      category.name.toLowerCase().includes(categorySearch.trim().toLowerCase()),
    ),
  );

  let activeFilterCount = $derived(statusFilter.length + categoryFilter.length);

  function toggleStatus(status: TaskStatus) {
    statusFilter = statusFilter.includes(status)
      ? statusFilter.filter((s) => s !== status)
      : [...statusFilter, status];
  }

  function toggleCategory(category: Category) {
    categoryFilter = categoryFilter.some((c) => c.id === category.id)
      ? categoryFilter.filter((c) => c.id !== category.id)
      : [...categoryFilter, category];
  }

  function clearFilters() {
    statusFilter = [];
    categoryFilter = [];
  }

  let visibleTasks = $derived(
    tasks.filter((t) => {
      const statusOk =
        statusFilter.length === 0 || statusFilter.includes(t.itemStatus);

      const categoryOk =
        categoryFilter.length === 0 ||
        t.categories?.some((tc) =>
          categoryFilter.some((cf) => cf.id === tc.id),
        );

      return statusOk && categoryOk;
    }),
  );

  let taskById = $derived(new Map(visibleTasks.map((t) => [String(t.id), t])));

  let events = $derived(
    visibleTasks
      .filter((t) => t.dueDate)
      .flatMap((t) => taskToEvents(t, currentView)),
  );

  const calendar = useCalendarApp({
    views: [
      createDayView({ hourHeight: 32 }),
      createWeekView({ gridDateClick: "day-view", hourHeight: 32 }),
      createMonthView({ gridDateClick: "day-view" }),
    ],
    defaultView: ViewType.MONTH,
    plugins: [createEventsPlugin()],
    events: [],
    theme: {
      mode: $theme === "dark" ? "dark" : "light",
    },
    calendars: statusCalendars,
    callbacks: {
      onEventClick: (event) => {
        const task = taskById.get(taskIdFromEventId(event.id));
        if (task) onSelectTask(task.id);
      },
      onEventCreate: (event) => {
        if (syncingEvents) return;
        const start = new Date(event.start.toString());
        onAddTask(start);
      },
      onMoreEventsClick: (date) => {
        calendar.selectDate(date);
        calendar.changeView(ViewType.DAY);
      },
      onEventDoubleClick: () => {
        return false;
      },
      onViewChange: (view) => {
        currentView = view === ViewType.MONTH;
      },
    },
  });

  let currentView = $state(calendar.currentView === "month");

  // Credit to claude code, this breaks up multiday events into seperate events and makes the days in between all day events only if not in month view
  // Stays the same in month view because it looks good but in day and week view this makes it far more readable
  function taskToEvents(t: Task, monthView: boolean): Event[] {
    const calendarId = statusToCalendarId[t.itemStatus];

    const end = new Date(`${t.dueDate}T${t.dueTime ?? "23:59"}`);
    const endIsFullDay = end.getHours() === 23 && end.getMinutes() === 59;
    if (!t.startDate) {
      if (endIsFullDay) {
        return [
          createAllDayEvent({
            id: String(t.id),
            title: t.title,
            start: end,
            calendarId,
          }),
        ];
      } else {
        return [
          createEvent({
            id: String(t.id),
            title: t.title,
            start: new Date(`${t.dueDate}T${"00:00"}`),
            end: end,
            calendarId,
          }),
        ];
      }
    }

    const start = new Date(`${t.startDate}T${t.startTime ?? "00:00"}`);

    const isMultiDay = t.startDate !== t.dueDate;

    const startPlain = Temporal.PlainDate.from(t.startDate);
    const endPlain = Temporal.PlainDate.from(t.dueDate);

    const startIsFullDay = start.getHours() === 0 && start.getMinutes() === 0;

    if (!isMultiDay && startIsFullDay && endIsFullDay) {
      return [
        {
          id: String(t.id),
          title: t.title,
          start: startPlain,
          end: endPlain,
          allDay: true,
          calendarId,
        } as Event,
      ];
    }

    if (!isMultiDay || monthView) {
      return [
        createEvent({
          id: String(t.id),
          title: t.title,
          start,
          end,
          calendarId,
        }),
      ];
    }

    const segments: Event[] = [];

    let allDayStart = startPlain.add({ days: 1 });
    let allDayEnd = endPlain.subtract({ days: 1 });

    if (startIsFullDay) {
      allDayStart = startPlain;
    } else {
      const endOfStartDay = new Date(start);
      endOfStartDay.setHours(23, 59);
      segments.push(
        createEvent({
          id: `${t.id}::start`,
          title: t.title,
          start,
          end: endOfStartDay,
          calendarId,
        }),
      );
    }

    if (endIsFullDay) {
      allDayEnd = endPlain;
    } else {
      const startOfEndDay = new Date(end);
      startOfEndDay.setHours(0, 0);
      segments.push(
        createEvent({
          id: `${t.id}::end`,
          title: t.title,
          start: startOfEndDay,
          end,
          calendarId,
        }),
      );
    }

    if (Temporal.PlainDate.compare(allDayStart, allDayEnd) <= 0) {
      segments.push({
        id: `${t.id}::middle`,
        title: t.title,
        start: allDayStart,
        end: allDayEnd,
        allDay: true,
        calendarId,
      } as Event);
    }

    return segments;
  }

  function eventsAreEqual(a: Event, b: Event): boolean {
    return (
      a.title === b.title &&
      String(a.start) === String(b.start) &&
      String(a.end) === String(b.end) &&
      a.calendarId === b.calendarId
    );
  }

  $effect(() => {
    syncingEvents = true;
    const current = new Map(calendar.events.map((e) => [e.id, e]));
    const next = new Map(events.map((e) => [e.id, e]));

    for (const id of current.keys()) {
      if (!next.has(id)) calendar.deleteEvent(id);
    }
    for (const [id, event] of next) {
      const existing = current.get(id);
      if (!existing) {
        calendar.addEvent(event);
      } else if (!eventsAreEqual(existing, event)) {
        calendar.updateEvent(id, event);
      }
    }
    syncingEvents = false;
  });

  function taskIdFromEventId(eventId: string): string {
    return eventId.split("::")[0];
  }

  let syncingEvents = false; // need this to guard the callback so it ignores adds that come from the sync event and not from a user action

  onMount(async () => {
    try {
      categories = await fetchCategories();
    } finally {
      categoriesLoading = false;
    }
  });
</script>

<div class="flex flex-col-reverse lg:flex-row w-full gap-4">
  <div
    class="bg-surface flex flex-col items-stretch gap-6 shadow-sm border border-surface-alt lg:w-94 rounded-lg p-4 shrink-0"
  >
    <div>
      <div class="flex items-center justify-between">
        <h2 class="text-2xl font-semibold">Filter</h2>
        <button
          class="secondary-button inline-flex items-center gap-2 text-sm text-red-500"
          disabled={activeFilterCount === 0}
          onclick={clearFilters}
        >
          Clear Filter{activeFilterCount > 1 ? "s" : ""}{activeFilterCount
            ? ` (${activeFilterCount})`
            : ""}
          <Xicon size={1} />
        </button>
      </div>
    </div>

    <details open class="flex flex-col gap-2">
      <summary class="cursor-pointer select-none font-medium">Status</summary>
      <hr class="w-full border-surface-alt" />
      <div class="inline-flex gap-2 flex-wrap mt-2 items-center justify-center">
        {#each [TaskStatus.Todo, TaskStatus.InProgress, TaskStatus.Done] as status}
          <button
            type="button"
            class="secondary-button {statusFilter.includes(status)
              ? 'text-white'
              : 'bg-surface-alt! hover:bg-surface-alt-hover!'} border"
            style="background-color: {colourForStatus(
              status,
            )}; border-color: {colourForStatus(status)}"
            aria-pressed={statusFilter.includes(status)}
            onclick={() => toggleStatus(status)}
          >
            <div class="inline-flex gap-2 items-center">
              <TaskIcon
                {status}
                colour={statusFilter.includes(status) ? "white" : undefined}
              />
              {statusLabel(TaskStatus[status])}
            </div>
          </button>
        {/each}
      </div>
    </details>

    <details open class="flex flex-col gap-2">
      <summary class="cursor-pointer select-none font-medium"
        >Categories</summary
      >
      <hr class="w-full border-surface-alt" />
      {#if categoriesLoading}
        <div class="flex flex-col gap-2 mt-2" aria-busy="true">
          <div
            class="h-8 w-full rounded-sm bg-surface-alt animate-pulse motion-reduce:animate-none"
          ></div>
          <div
            class="h-8 w-2/3 rounded-sm bg-surface-alt animate-pulse motion-reduce:animate-none"
          ></div>
        </div>
      {:else if categories.length === 0}
        <div class="text-sm text-text-muted mt-2">No categories yet</div>
      {:else}
        <div class="mt-2 flex flex-col gap-2">
          <input
            type="search"
            bind:value={categorySearch}
            class="search-field"
            placeholder="Search categories..."
            aria-label="Search categories"
          />

          {#if filteredCategories.length === 0}
            <div class="text-sm text-text-muted">No matching categories</div>
          {:else}
            <div class="relative">
              <div class="overflow-y-auto max-h-80 pb-4">
                <div class="inline-flex gap-2 flex-wrap">
                  {#each filteredCategories as category}
                    <button
                      type="button"
                      class="secondary-button {categoryFilter.some(
                        (c) => c.id === category.id,
                      )
                        ? 'bg-brand text-surface'
                        : ''}"
                      aria-pressed={categoryFilter.some(
                        (c) => c.id === category.id,
                      )}
                      onclick={() => {
                        toggleCategory(category);
                      }}
                    >
                      {category.name}
                    </button>
                  {/each}
                </div>
              </div>
            </div>
          {/if}
        </div>
      {/if}
    </details>

    {#if activeFilterCount > 0 && visibleTasks.length === 0}
      <div class="text-center text-text-muted p-4">
        No tasks match this filter
      </div>
    {/if}
  </div>

  <div class="flex-1 relative">
    <DayFlowCalendar mobileEventDetail={() => {}} {calendar} />
  </div>
</div>
