<script lang="ts">
  import { onMount } from "svelte";
  import { goto } from "$app/navigation";
  import { resolve } from "$app/paths";
  import { page } from "$app/state";
  import TaskFormComponent from "$lib/components/tasks/TaskForm.svelte";
  import { fetchTask, patchTask } from "$lib/api/tasks";
  import { type Task, type TaskForm } from "$lib/types";
  import BasePage from "$lib/components/utils/BasePage.svelte";

  const taskId = Number(page.params.slug);

  let task = $state<Task | null>(null);
  let loading = $state(true);
  let loadError = $state("");

  function taskToForm(t: Task): TaskForm {
    return {
      id: t.id,
      title: t.title,
      description: t.description ?? "",
      itemStatus: t.itemStatus,
      dueDate: t.dueDate ?? "",
      dueTime: t.dueTime ? t.dueTime.slice(0, 5) : "",
      startDate: t.startDate ?? "",
      startTime: t.startTime ? t.startTime.slice(0, 5) : "",
      taskAttachments: t.taskAttachments ?? [],
      parentTaskId: t.parentTaskId,
      categoryIds: (t.categories ?? []).map((c) => c.id),
      categories: t.categories ?? [],
      visibility: t.visibility,
    };
  }

  onMount(async () => {
    try {
      task = await fetchTask(taskId);
    } catch (err) {
      loadError = err instanceof Error ? err.message : "Failed to load task";
    } finally {
      loading = false;
    }
  });

  const handleSubmit = async (data: TaskForm): Promise<string> => {
    try {
      await patchTask(taskId, data);
      goto(resolve(`/tasks/${taskId}`));
      return "";
    } catch (err) {
      return err instanceof Error ? err.message : "Failed to update task";
    }
  };
</script>

<BasePage maxWidth="max-w-4xl" class="md:p-8">
  <section class="card">
    <h1 class="font-semibold font-serif text-left">Edit Task</h1>
    {#if loading}
      <p class="mt-4 text-sm text-text-muted">Loading task...</p>
    {:else if loadError}
      <p class="mt-4 text-sm text-red-500">{loadError}</p>
    {:else if task}
      <div class="mt-6">
        <TaskFormComponent
          initialTask={taskToForm(task)}
          onSubmit={handleSubmit}
          onCancel={() => goto(resolve(`/tasks/${taskId}`))}
        />
      </div>
    {/if}
  </section>
</BasePage>
