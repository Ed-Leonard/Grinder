import { fetchWithCsrf } from "$lib/csrf";
import { resolve } from "$app/paths";
import type { Task } from "$lib/types";

export interface Category {
  id: number;
  name: string;
  tasks: Array<Task>;
}

export interface CreateCategoryRequest {
  name: string;
}

export async function fetchCategories(): Promise<Category[]> {
  const response = await fetchWithCsrf(resolve(`/api/category` as any), {
    method: "GET",
    credentials: "include",
  });

  if (!response.ok) {
    throw new Error("Failed to fetch categories");
  }

  return await response.json();
}

export async function createCategory(name: string): Promise<Category> {
  const response = await fetchWithCsrf(resolve(`/api/category` as any), {
    method: "POST",
    credentials: "include",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify({ name }),
  });

  if (!response.ok) {
    throw new Error("Failed to create category");
  }

  const text = await response.text();
  if (!text) {
    const categories = await fetchCategories();
    const created = categories.find(
      (c) => c.name.toLowerCase() === name.toLowerCase(),
    );
    if (!created) {
      throw new Error("Category was created but could not be found afterward");
    }
    return created;
  }

  return JSON.parse(text);
}
