<script lang="ts">
  import TaskFormComponent from "$lib/components/tasks/TaskForm.svelte";
  import { type TaskForm, type Task, TaskStatus } from "$lib/types";
  import { goto } from "$app/navigation";
  import { resolve } from "$app/paths";
  import { createTask } from "$lib/api/tasks";
  import { page } from "$app/state";
  import { user } from "$lib/stores/auth";
  import BasePage from "$lib/components/utils/BasePage.svelte";

  const handleSubmit = async (task: TaskForm): Promise<string> => {
    try {
      const createdTask: Task = await createTask(task);
      goto(resolve(`/tasks/${createdTask.id}`));
      return "";
    } catch (err) {
      return err instanceof Error ? err.message : "Failed to create task";
    }
  };
</script>

<BasePage maxWidth="max-w-4xl" class="md:p-8">
  <section class="card">
    <h1 class="font-semibold font-serif text-left">Create Task</h1>

    <div class="mt-6">
      <TaskFormComponent
        onSubmit={handleSubmit}
        onCancel={() => goto(resolve(`/profile/${$user?.id}`))}
        parentTaskId={page.url.searchParams.get("parent")}
      />
    </div>
  </section>
</BasePage>
