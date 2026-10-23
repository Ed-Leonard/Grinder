import { fetchWithCsrf } from "$lib/csrf";
import type { Task, TaskAttachment, ParentTask, TaskForm } from "$lib/types";
import { resolve } from "$app/paths";
import { getImageDimensions, getVideoDimensions } from "$lib/utils/aspectRatio";
import { notifyTaskUpdated } from "$lib/stores/taskUpdate";

type ApiErrorResponse = {
  message?: string;
  errors?: Record<string, string[]>;
};

const MAX_IMAGE_FILE_SIZE = 5 * 1024 * 1024;
const MAX_VIDEO_FILE_SIZE = 50 * 1024 * 1024;

const SUPPORTED_IMAGE_TYPES: ReadonlySet<string> = new Set([
  "image/png",
  "image/jpeg",
  "image/svg+xml",
  "image/gif",
]);

const SUPPORTED_VIDEO_TYPES: ReadonlySet<string> = new Set([
  "video/mp4",
  "video/quicktime",
  "video/webm",
]);

function getAttachmentSizeError(file: File): string | null {
  if (SUPPORTED_IMAGE_TYPES.has(file.type) && file.size > MAX_IMAGE_FILE_SIZE) {
    return "Image is too large, please choose a smaller file";
  }

  if (SUPPORTED_VIDEO_TYPES.has(file.type) && file.size > MAX_VIDEO_FILE_SIZE) {
    return "Video is too large, please choose a smaller file";
  }

  return null;
}

async function getApiErrorMessage(
  response: Response,
  fallback: string,
): Promise<string> {
  try {
    const errorResponse: ApiErrorResponse = await response.json();

    if (errorResponse.message) {
      return errorResponse.message;
    }

    if (errorResponse.errors) {
      const messages: string[] = Object.values(errorResponse.errors).flat();
      if (messages.length > 0) {
        return messages[0];
      }
    }
  } catch {
    // The API did not return a JSON error body.
  }

  return fallback;
}

export async function uploadTemporaryAttachment(
  file: File,
): Promise<TaskAttachment> {
  const sizeError: string | null = getAttachmentSizeError(file);
  if (sizeError) {
    throw new Error(sizeError);
  }
  const formData: FormData = new FormData();
  formData.append("file", file);

  try {
    const dimensions = file.type.startsWith("video/")
      ? await getVideoDimensions(file)
      : file.type.startsWith("image/")
        ? await getImageDimensions(file)
        : null;
    if (dimensions) {
      formData.append("width", String(dimensions.width));
      formData.append("height", String(dimensions.height));
      console.log(dimensions);
    }
  } catch {
    // Non-fatal — upload proceeds, backend/frontend just treat it as
    // "unknown" aspect ratio for grid layout purposes.
  }

  let response: Response;
  try {
    response = await fetchWithCsrf(resolve(`/api/attachmentuploads` as any), {
      method: "POST",
      credentials: "include",
      body: formData,
    });
  } catch {
    throw new Error("Failed to upload attachment");
  }
  if (!response.ok) {
    const message: string = await getApiErrorMessage(
      response,
      "Failed to upload attachment",
    );
    throw new Error(message);
  }
  return (await response.json()) as TaskAttachment;
}

export async function deleteAttachment(attachmentId: number): Promise<void> {
  const response: Response = await fetchWithCsrf(
    resolve(`/api/attachmentuploads/${attachmentId}` as any),
    {
      method: "DELETE",
      credentials: "include",
    },
  );

  if (!response.ok && response.status !== 404) {
    const message: string = await getApiErrorMessage(
      response,
      "Failed to remove attachment",
    );
    throw new Error(message);
  }
}

// utility function to flatten tree into array of tasks for calendar etc.
export function flattenTasks(tasks: Task[]): Task[] {
  const result: Task[] = [];
  for (const task of tasks) {
    result.push(task);
    if (task.children?.length) {
      result.push(...flattenTasks(task.children));
    }
  }
  return result;
}

export async function fetchTasks(userId?: number): Promise<Task[]> {
  const response = await fetchWithCsrf(
    resolve(
      (userId != null ? `/api/tasks/user/${userId}` : `/api/tasks/user`) as any,
    ),
    {
      method: "GET",
      credentials: "include",
    },
  );

  if (!response.ok) {
    throw new Error("Failed to fetch tasks");
  }

  return await response.json();
}

export async function fetchParents(): Promise<ParentTask[]> {
  const response = await fetchWithCsrf(resolve(`/api/tasks/parents` as any), {
    method: "GET",
    credentials: "include",
  });

  if (!response.ok) {
    throw new Error("Failed to fetch tasks");
  }

  return await response.json();
}

export async function createTask(payload: TaskForm): Promise<Task> {
  const { taskAttachments, ...rest } = payload;

  const body = {
    ...rest,
    dueDate: payload.dueDate || null,
    dueTime: payload.dueTime || null,
    startTime: payload.startTime || null,
    startDate: payload.startDate || null,
    attachmentIds: (taskAttachments ?? []).map((attachment) => attachment.id),
  };

  const response = await fetchWithCsrf(resolve(`/api/tasks` as any), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    credentials: "include",
    body: JSON.stringify(body),
  });

  if (!response.ok) {
    let message = "Failed to create task";
    try {
      const errorData = await response.json();
      if (errorData.errors) {
        const messages = Object.values(errorData.errors).flat() as string[];
        if (messages.length > 0) message = messages[0];
      }
    } catch {}
    throw new Error(message);
  }

  notifyTaskUpdated();
  return await response.json();
}

export async function fetchTask(id: number): Promise<Task> {
  const response = await fetchWithCsrf(resolve(`/api/tasks/${id}` as any), {
    method: "GET",
    credentials: "include",
  });

  if (!response.ok) {
    throw new Error(
      response.status === 404 ? "Task not found" : "Failed to fetch task",
    );
  }

  return await response.json();
}

export async function patchTask(
  id: number,
  payload: Partial<TaskForm>,
): Promise<void> {
  const { taskAttachments, ...rest } = payload;
  const body: Record<string, unknown> = { ...rest };

  if (taskAttachments !== undefined) {
    body.attachmentIds = taskAttachments
      .filter((attachment) => attachment.taskItemId === null)
      .map((attachment) => attachment.id);
  }
  const response = await fetchWithCsrf(resolve(`/api/tasks/${id}` as any), {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    credentials: "include",
    body: JSON.stringify(body),
  });

  if (!response.ok) {
    let message = "Failed to update task";
    try {
      const errorData = await response.json();
      if (errorData.errors) {
        const messages = Object.values(errorData.errors).flat() as string[];
        if (messages.length > 0) message = messages[0];
      }
    } catch {}
    throw new Error(message);
  } else {
    notifyTaskUpdated();
  }
}

export async function deleteTask(id: number): Promise<void> {
  const response = await fetchWithCsrf(resolve(`/api/tasks/${id}` as any), {
    method: "DELETE",
    credentials: "include",
  });

  if (!response.ok) {
    throw new Error(
      response.status === 404 ? "Task not found" : "Failed to delete task",
    );
  }
  notifyTaskUpdated();
}

export async function updateTaskOrder(tasks: Task[], parentId: number) {
  await fetchWithCsrf(resolve(`/api/tasks/reorder` as any), {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    credentials: "include",
    body: JSON.stringify(
      tasks.map((t, i) => ({ id: t.id, order: i, parentId })),
    ),
  });
}

export async function getStatusCount(userId: number) {
  const response = await fetchWithCsrf(
    resolve(`/api/tasks/status-count/${userId}` as any),
    {
      method: "GET",
      credentials: "include",
    },
  );

  if (!response.ok) {
    throw new Error(
      "Failed to fetch status count for user " + userId.toString(),
    );
  }

  return await response.json();
}
