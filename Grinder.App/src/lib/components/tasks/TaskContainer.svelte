<script lang="ts">
  import { type Task, colourForStatus } from "$lib/types";
  import { updateTaskOrder } from "$lib/api/tasks";
  import TaskIcon from "./TaskIcon.svelte";
  import TaskDate from "./TaskDate.svelte";
  import ErrorPopup from "$lib/components/utils/Error.svelte";
  import Uprightarrow from "$lib/components/svgs/uprightarrow.svelte";
  import { patchTask } from "$lib/api/tasks";
  import Pluscircle from "$lib/components/svgs/pluscircle.svelte";
  import { DndManager } from "$lib/dnd.svelte";
  import { flip } from "svelte/animate";
  import { draggable, droppable } from "@thisux/sveltednd";
  import { goto } from "$app/navigation";
  import { resolve } from "$app/paths";
  import { toast } from "$lib/components/toasts/toast.svelte";
  import UndoRedo from "$lib/components/UndoRedo.svelte";
  import { untrack } from "svelte";
  import { tooltip } from "$lib/utils/tooltip";

  let {
    tasks,
    onTaskUpdate,
    onTasksReordered,
  }: {
    tasks: Task[];
    onTaskUpdate: (updatedTask: Task) => void;
    onTasksReordered: (listId: number, items: Task[]) => void;
  } = $props();

  let error: string | null = $state(null);

  $effect(() => {
    const currentTasks = tasks;

    untrack(() => {
      for (const parent of currentTasks) {
        dnd.setItems(parent.id, parent.children);
      }
    });
  });

  let expanded = $state(new Set<number>());
  function toggleExpanded(id: number) {
    if (expanded.has(id)) expanded.delete(id);
    else expanded.add(id);
    expanded = new Set(expanded);
  }

  const updateTaskStatus = async (task: Task) => {
    const previousStatus = task.itemStatus;
    const nextStatus = ((task.itemStatus + 1) % 3) as Task["itemStatus"];
    const updatedTask = { ...task, itemStatus: nextStatus };

    onTaskUpdate(updatedTask);

    try {
      await patchTask(task.id, { itemStatus: nextStatus });
    } catch (e) {
      onTaskUpdate({ ...task, itemStatus: previousStatus });
      toast("Failed to update task status");
    }
  };

  const dnd = new DndManager<Task>({
    getItemId: (task) => task.id,

    parseContainer: (container) => {
      const [l, t] = container.split("-").map(Number);
      return isNaN(l) || isNaN(t) ? null : { listId: l, itemId: t };
    },

    // updates task order within parent and parent id if cross parent drag
    onCommit: (action) => {
      const toItems = dnd.getItems(action.toListId);
      updateTaskOrder(toItems, action.toListId);
      onTasksReordered(action.toListId, toItems);

      if (action.fromListId !== action.toListId) {
        const fromItems = dnd.getItems(action.fromListId);
        updateTaskOrder(fromItems, action.fromListId);
        onTasksReordered(action.fromListId, fromItems);
      }
    },

    // let children be dragged to different parents
    canDrop: () => true,
  });

  function handleUndo() {
    const action = dnd.undo();
    if (!action)
      return toast(
        dnd.hitLimit
          ? "Cannot undo more than 20 changes"
          : "No sorting to undo",
      );

    updateTaskOrder(dnd.getItems(action.toListId), action.toListId);
    if (action.fromListId !== action.toListId) {
      updateTaskOrder(dnd.getItems(action.fromListId), action.fromListId);
    }
  }

  function handleRedo() {
    const action = dnd.redo();
    if (!action) return toast("No sorting to redo");

    updateTaskOrder(dnd.getItems(action.toListId), action.toListId);
    if (action.fromListId !== action.toListId) {
      updateTaskOrder(dnd.getItems(action.fromListId), action.fromListId);
    }
  }
</script>

{#if error}
  <ErrorPopup {error} />
{:else}
  <div class="inline-flex justify-between items-center w-full p-4">
    <UndoRedo {handleUndo} {handleRedo} />
    <a
      class="primary-button inline-flex gap-2 items-center"
      href={resolve("/tasks/new")}
    >
      <Pluscircle /> Create Task
    </a>
  </div>
  <div class="grid items-start gap-4 lg:grid-cols-2 p-4">
    {#each tasks as parent (parent.id)}
      <div
        class="card rounded-lg p-4 border-surface-alt-hover relative parent-group min-w-0 [&:has(.parent-link:hover),&:has(.parent-drop:hover)]:bg-surface-hover transition-colors flex flex-col gap-2"
        style="border-left: 4px solid {colourForStatus(parent.itemStatus)}"
      >
        <a
          href={resolve(`/tasks/${parent.id}`)}
          class="absolute inset-0 z-0 parent-link"
          aria-label={`Go to ${parent.title}`}
        ></a>
        <div class="inline-flex justify-between w-full items-center">
          <h2 class="truncate font-semibold font-serif">
            {parent.title}
          </h2>
          <div class="inline-flex gap-2 items-center shrink-0">
            <div class="parent-arrow-icon hidden">
              <Uprightarrow />
            </div>
            <TaskDate
              date={parent.dueDate}
              time={parent.dueTime}
              className="text-sm text-text-muted"
            />
            <button
              onclick={() => updateTaskStatus(parent)}
              class="relative z-10 icon-button"
              use:tooltip={"Update task status"}
            >
              <TaskIcon status={parent.itemStatus} size={1.2} />
            </button>
          </div>
        </div>
        <p class="text-md ml-2 line-clamp-4 wrap-anywhere">
          {parent.description}
        </p>
        <div class="mt-2 flex flex-col">
          {#each dnd.getViewItems(parent.id) as child (child.id)}
            {@const containerId = `${parent.id}-${child.id}`}
            <div
              class="lg:mx-2 py-1 child-group z-10"
              class:opacity-30={dnd.isGhost(parent.id, child.id)}
              animate:flip={{ duration: 200 }}
              use:draggable={{
                container: containerId,
                dragData: child,
                attributes: { draggingClass: "cursor-grabbing" },
                callbacks: {
                  onDragStart: dnd.handleDragStart,
                  onDragEnd: dnd.handleDragEnd,
                },
              }}
              use:droppable={{
                container: containerId,
                callbacks: { onDragOver: dnd.handleDragOver },
              }}
            >
              <div
                class="p-2 cursor-pointer relative z-10 rounded-lg bg-surface border border-surface-alt [&:has(.child-link:hover)]:bg-surface-hover transition-colors"
                style="border-left: 3px solid {colourForStatus(
                  child.itemStatus,
                )}"
              >
                <!-- anchor tag hijacks all drag events so we have to do manually -->
                <div
                  class="child-link absolute inset-0 z-0"
                  role="link"
                  tabindex="0"
                  aria-label={`Go to ${child.title}`}
                  onclick={() => goto(resolve(`/tasks/${child.id}`))}
                  onkeydown={(e) =>
                    e.key === "Enter" && goto(resolve(`/tasks/${child.id}`))}
                ></div>

                <div class="flex items-center justify-between">
                  <div class="inline-flex gap-1 items-baseline min-w-0 flex-1">
                    {#if child.children.length > 0}
                      <button
                        class="min-w-0 max-w-130 shrink py-1 text-md text-text-muted hover:text-text inline-flex gap-1 items-baseline relative z-10"
                        onclick={() => toggleExpanded(child.id)}
                        use:tooltip={"Expand subtasks"}
                      >
                        {expanded.has(child.id) ? "▾" : "▸"}
                        <h4 class="min-w-0 truncate">{child.title}</h4>
                      </button>
                    {:else}
                      <h4 class="min-w-0 max-w-130 shrink truncate py-1">
                        {child.title}
                      </h4>
                    {/if}
                  </div>
                  <div class="inline-flex gap-2 items-center shrink-0">
                    <a
                      href={resolve(`/tasks/new?parent=${child.id}`)}
                      class="child-arrow-icon icon-button hidden relative z-10"
                      use:tooltip={"Create Subtask"}
                    >
                      <Pluscircle />
                    </a>
                    <div class="child-arrow-icon hidden">
                      <Uprightarrow />
                    </div>
                    <TaskDate
                      date={child.dueDate}
                      time={child.dueTime}
                      className="text-text-muted text-sm shrink-0"
                    />
                    <button
                      onclick={() => updateTaskStatus(child)}
                      class="icon-button relative z-10"
                      use:tooltip={"Update task status"}
                    >
                      <TaskIcon status={child.itemStatus} />
                    </button>
                  </div>
                </div>
                {#if child.children.length > 0}
                  {#if expanded.has(child.id)}
                    <div class="mt-1 ml-3 flex flex-col">
                      {#each child.children as grandchild}
                        <div class="z-20 grandchild-group py-0.5">
                          <div
                            class="flex items-center justify-between text-sm p-2 rounded-md relative z-20 border border-surface-alt [&:has(.grandchild-link:hover)]:bg-surface-hover bg-surface transition-colors"
                            style="border-left: 2px solid {colourForStatus(
                              grandchild.itemStatus,
                            )}"
                          >
                            <a
                              href={resolve(`/tasks/${grandchild.id}`)}
                              class="absolute inset-0 z-0 grandchild-link"
                              aria-label={`Go to ${grandchild.title}`}
                            ></a>
                            <h5 class="truncate">{grandchild.title}</h5>
                            <p class="text-xs truncate mx-4 min-w-0 flex-1">
                              {grandchild.description}
                            </p>
                            <div
                              class="inline-flex gap-2 items-center shrink-0"
                            >
                              <div class="grandchild-arrow-icon hidden">
                                <Uprightarrow />
                              </div>
                              <TaskDate
                                date={grandchild.dueDate}
                                time={grandchild.dueTime}
                                className="text-text-muted text-xs shrink-0"
                              />
                              <button
                                onclick={() => updateTaskStatus(grandchild)}
                                class="icon-button relative z-10"
                                use:tooltip={"Update task status"}
                              >
                                <TaskIcon
                                  status={grandchild.itemStatus}
                                  size={1}
                                />
                              </button>
                            </div>
                          </div>
                        </div>
                      {/each}
                    </div>
                  {/if}
                {/if}
              </div>
            </div>
          {/each}
          {#if dnd.getViewItems(parent.id).length === 0}
            <a
              class="w-full min-h-15.5 rounded-lg border border-dashed border-surface-alt-hover z-10 parent-drop"
              href={resolve(`/tasks/${parent.id}`)}
              aria-label="Go to task page"
              use:droppable={{
                container: `${parent.id}-0`,
                callbacks: {
                  onDragOver: dnd.handleDragOver,
                },
              }}
            ></a>
          {/if}
          <a
            class="child-group lg:mx-8 mt-4 secondary-button inline-flex gap-2 items-center justify-center z-10 border border-surface-alt-hover"
            href={resolve(`/tasks/new?parent=${parent.id}`)}
          >
            <Pluscircle />
            Create Subtask
          </a>
        </div>
      </div>
    {/each}
  </div>
{/if}

<style>
  .parent-group:hover:not(:has(.child-group:hover), :has(button:hover))
    .parent-arrow-icon {
    display: block;
  }

  .child-group:hover:not(:has(.grandchild-group:hover), :has(button:hover))
    .child-arrow-icon {
    display: block;
  }

  .grandchild-group:hover:not(:has(.child-group:hover), :has(button:hover))
    .grandchild-arrow-icon {
    display: block;
  }
</style>
