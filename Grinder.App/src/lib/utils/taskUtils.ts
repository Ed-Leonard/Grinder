import { TaskStatus } from "$lib/types";

export function colourForStatus(status: TaskStatus): string {
  switch (status) {
    case TaskStatus.InProgress:
      return "#f4a261";
    case TaskStatus.Done:
      return "#10b981";
    default:
      return "#6c757d";
  }
}

export function statusLabel(status: TaskStatus): string {
  return status === TaskStatus.InProgress ? "In progress" : TaskStatus[status];
}

export function statusBadgeClass(status: TaskStatus): string {
  switch (status) {
    case TaskStatus.Done:
      return "bg-[#10b981] text-black/80";
    case TaskStatus.InProgress:
      return "bg-[#f4a261] text-black/80";
    default:
      return "bg-[#6c757d] text-surface";
  }
}
