import { error } from "@sveltejs/kit";
import type { PageLoad } from "./$types";
import type { Task } from "$lib/types";
import { resolve } from "$app/paths";

export const load: PageLoad = async ({ params, fetch }) => {
  const res = await fetch(resolve(`/api/tasks/${params.slug}` as any));
  if (!res.ok) {
    error(404, `Task not found: ${params.slug}`);
  }
  const task: Task = await res.json();

  const ancestors: { id: number; title: string }[] = [];
  let currentParentId = task.parentTaskId;

  while (currentParentId !== null && currentParentId !== undefined) {
    const parentRes = await fetch(
      resolve(`/api/tasks/${currentParentId}` as any),
    );
    if (!parentRes.ok) break;
    const parentTask: Task = await parentRes.json();
    ancestors.unshift({ id: parentTask.id, title: parentTask.title });
    currentParentId = parentTask.parentTaskId;
  }

  return { task, ancestors };
};
