import type { Category } from "./api/category";
import type { ReactionResponse } from "./api/reaction";

export interface TaskAttachment {
  id: number;
  taskItemId: number | null;
  originalFileName: string;
  contentType: string;
  fileSize: number;
  width: number | null;
  height: number | null;
  createdAt: string;
}

export enum TaskStatus {
  Todo,
  InProgress,
  Done,
}

export enum UpdatedFields {
  Title,
  Description,
  Start,
  Due,
  Status,
  Categories,
  ParentTask,
}

export interface Task {
  id: number;
  parentTaskId: number | null;
  userId: number;
  title: string;
  description: string | null;
  createdAt: string;
  startDate: string | null;
  startTime: string | null;
  dueDate: string;
  dueTime: string;
  itemStatus: TaskStatus;
  visibility: TaskVisibility;
  order: number;
  taskAttachments: TaskAttachment[];
  children: Task[];
  categories?: Category[];
  reactions: ReactionResponse[];
  depth: number;
  isClip?: boolean;
}

export enum TaskVisibility {
  Private,
  FriendsOnly,
  Public,
}

export function statusLabel(status: string): string {
  return status === "InProgress" ? "In Progress" : status;
}

export const statusConfig: Record<TaskStatus, { colour: string }> = {
  [TaskStatus.Todo]: {
    colour: "#6c757d",
  },
  [TaskStatus.InProgress]: {
    colour: "#f4a261",
  },
  [TaskStatus.Done]: {
    colour: "#10b981",
  },
};

export const colourForStatus = (s: TaskStatus) => statusConfig[s].colour;

export interface FormData {
  email: string;
  name: string;
  password: string;
  country: string;
}

export interface TaskForm {
  id?: number;
  title: string;
  description: string;
  startDate: string | null;
  startTime: string | null;
  dueDate: string;
  taskAttachments: TaskAttachment[];
  dueTime: string | null;
  itemStatus: TaskStatus;
  visibility: TaskVisibility;
  parentTaskId: number | null;
  categoryIds?: number[];
  categories?: Category[];
}

export const defaultForm: TaskForm = {
  title: "",
  description: "",
  startDate: "",
  startTime: "",
  dueDate: "",
  dueTime: "",
  itemStatus: 0,
  visibility: TaskVisibility.Private,
  parentTaskId: null,
  categoryIds: [],
  taskAttachments: [],
};

export type ParentTask = {
  id: number;
  title: string;
};

export type SearchResponse = {
  users: UserSearchResult[];
  tasks: Task[];
  page: number;
  totalPages: number;
};

export type UserSearchResult = {
  id: number;
  displayName: string;
  relationshipStatus: RelationshipStatus;
};

export enum RelationshipStatus {
  Follow = "Follow",
  Following = "Following",
  Friends = "Friends",
  FollowBack = "FollowBack",
}

export const relationshipStatusLabel: Record<RelationshipStatus, string> = {
  [RelationshipStatus.Follow]: "Follow",
  [RelationshipStatus.Following]: "Following",
  [RelationshipStatus.Friends]: "Friends",
  [RelationshipStatus.FollowBack]: "Follow Back",
};

export type User = {
  id: number;
  email: string;
  displayName: string;
  country: string;
};
