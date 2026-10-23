import { fetchWithCsrf } from "$lib/csrf";
import { resolve } from "$app/paths";

export interface CommentAuthor {
  id: number;
  displayName: string;
  profilePictureUrl: string;
}

export interface CommentResponse {
  id: number;
  taskId: number;
  authorId: number;
  author: CommentAuthor;
  content: string;
  createdAt: string;
  parentCommentId: number | null;
  isDeleted: boolean;
  replyCount: number;
  replies: CommentResponse[];
}

export interface TaskCommentsResponse {
  comments: CommentResponse[];
  totalCount: number;
}

export interface PaginatedRepliesResponse {
  replies: CommentResponse[];
  totalCount: number;
  hasMore: boolean;
}

export interface CreateCommentRequest {
  taskId: number;
  content: string;
  parentCommentId?: number | null;
}

export interface DeleteCommentResult {
  removed: boolean;
  comment?: CommentResponse;
}

type ApiErrorResponse = {
  message?: string;
  errors?: Record<string, string[]>;
};

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
  } catch {}
  return fallback;
}

export async function getCommentsByTaskId(
  taskId: number,
): Promise<TaskCommentsResponse> {
  const response: Response = await fetchWithCsrf(
    resolve(`/api/Comment/task/${taskId}` as any),
    {
      method: "GET",
      credentials: "include",
    },
  );

  if (!response.ok) {
    const message: string = await getApiErrorMessage(
      response,
      "Failed to get comments",
    );
    throw new Error(message);
  }

  return await response.json();
}

export async function getRepliesByCommentId(
  commentId: number,
  skip: number = 0,
  take: number = 10,
): Promise<PaginatedRepliesResponse> {
  const response: Response = await fetchWithCsrf(
    resolve(
      `/api/Comment/comment/${commentId}?skip=${skip}&take=${take}` as any,
    ),
    {
      method: "GET",
      credentials: "include",
    },
  );

  if (!response.ok) {
    const message: string = await getApiErrorMessage(
      response,
      "Failed to get replies",
    );
    throw new Error(message);
  }

  return await response.json();
}

export async function createComment(
  request: CreateCommentRequest,
): Promise<CommentResponse> {
  const response: Response = await fetchWithCsrf(
    resolve("/api/Comment" as any),
    {
      method: "POST",
      credentials: "include",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify(request),
    },
  );

  if (!response.ok) {
    if (response.status === 400) {
      throw new Error("Comment must be 2048 characters or less");
    }
    const message: string = await getApiErrorMessage(
      response,
      "Failed to create comment",
    );
    throw new Error(message);
  }

  return await response.json();
}

export async function deleteComment(
  commentId: number,
): Promise<DeleteCommentResult> {
  const response: Response = await fetchWithCsrf(
    resolve(`/api/Comment/${commentId}` as any),
    {
      method: "DELETE",
      credentials: "include",
    },
  );

  if (!response.ok) {
    const message: string = await getApiErrorMessage(
      response,
      "Failed to delete comment",
    );
    throw new Error(message);
  }

  return await response.json();
}
