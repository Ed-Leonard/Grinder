<script lang="ts">
  import {
    type TaskForm,
    TaskStatus,
    defaultForm,
    TaskVisibility,
    type ParentTask,
    type TaskAttachment,
  } from "$lib/types";
  import {
    createDueDateTimeValidator,
    getLocalisedDateFormat,
    createStartDateTimeValidator,
  } from "$lib/utils/dateUtils.svelte";
  import { onMount } from "svelte";
  import {
    deleteAttachment,
    fetchParents,
    uploadTemporaryAttachment,
  } from "$lib/api/tasks";
  import { diffObjects } from "$lib/utils/compare";
  import Combobox from "$lib/components/FormFields/Combobox.svelte";
  import ErrorPopup from "$lib/components/utils/Error.svelte";
  import CategoryList from "$lib/components/Categories/CategoryList.svelte";
  import { createCategory } from "$lib/api/category";
  import type { Category } from "$lib/api/category";
  import ConfirmAttachmentsModal from "$lib/components/media/ConfirmAttachmentsModal.svelte";
  import { taskAttachmentUrl } from "$lib/api/media";
  import { statusLabel, colourForStatus } from "$lib/types";
  import TaskIcon from "./TaskIcon.svelte";
  import Pluscircle from "../svgs/pluscircle.svelte";
  import autogrow from "$lib/utils/autogrow";
  import Xicon from "../svgs/xicon.svelte";
  import Lock from "../svgs/visibility/lock.svelte";
  import UserGroup from "../svgs/visibility/usergroup.svelte";
  import Globe from "../svgs/visibility/globe.svelte";
  import { tooltip } from "$lib/utils/tooltip";
  import Visibility from "../FormFields/Visibility.svelte";

  let {
    initialTask,
    onSubmit,
    onCancel,
    parentTaskId,
  }: {
    initialTask?: TaskForm;
    onSubmit: (data: TaskForm) => Promise<string>;
    onCancel: () => void;
    parentTaskId?: string | null;
  } = $props();

  // intentionally captures initial state as its all we need to confirm if editing or not
  let editing = initialTask != null;

  let parents = $state<ParentTask[]>([]);

  let selectedCategories: Category[] = $state(initialTask?.categories ?? []);

  let task = $state<TaskForm>(
    initialTask
      ? { ...initialTask }
      : {
          ...defaultForm,
          parentTaskId: parentTaskId ? Number(parentTaskId) : null,
        },
  );

  let submitted = $state(false);
  let showConfirmModal = $state(false);
  let cancelling = $state(false);

  let stagedRemovalIds = $state<Set<number>>(new Set());

  let visibleAttachments = $derived(
    task.taskAttachments.filter(
      (a: TaskAttachment) => !stagedRemovalIds.has(a.id),
    ),
  );

  let startDateInput = $state<HTMLInputElement | null>(null);
  let startTimeInput = $state<HTMLInputElement | null>(null);

  let dueDateInput = $state<HTMLInputElement | null>(null);
  let dueTimeInput = $state<HTMLInputElement | null>(null);

  let error = $state("");

  let startDateChanged = $derived(
    !initialTask || task.startDate !== initialTask.startDate,
  );
  let startTimeChanged = $derived(
    !initialTask || task.startTime !== initialTask.startTime,
  );
  let dueDateChanged = $derived(
    !initialTask || task.dueDate !== initialTask.dueDate,
  );
  let dueTimeChanged = $derived(
    !initialTask || task.dueTime !== initialTask.dueTime,
  );

  const dueDateTime = createDueDateTimeValidator({
    getDate: () => task.dueDate,
    getTime: () => task.dueTime,
    setTime: (v) => (task.dueTime = v),
  });

  const startDateTime = createStartDateTimeValidator({
    getStartDate: () => task.startDate,
    getStartTime: () => task.startTime,
    setStartTime: (v) => (task.startTime = v),
    getDueDate: () => task.dueDate,
    getDueTime: () => task.dueTime,
  });

  function openAttachmentModal(): void {
    showConfirmModal = true;
  }

  async function uploadAttachments(files: File[]): Promise<void> {
    const uploadedAttachments: TaskAttachment[] = [];

    try {
      for (const file of files) {
        const attachment: TaskAttachment =
          await uploadTemporaryAttachment(file);
        uploadedAttachments.push(attachment);
      }

      task.taskAttachments = [...task.taskAttachments, ...uploadedAttachments];
      showConfirmModal = false;
    } catch (uploadError: unknown) {
      await Promise.allSettled(
        uploadedAttachments.map((attachment: TaskAttachment) =>
          deleteAttachment(attachment.id),
        ),
      );

      throw uploadError;
    }
  }

  function removeAttachment(attachment: TaskAttachment): void {
    const next = new Set(stagedRemovalIds);
    next.add(attachment.id);
    stagedRemovalIds = next;
  }

  async function commitStagedRemovals(): Promise<void> {
    if (stagedRemovalIds.size === 0) return;

    const toRemove = task.taskAttachments.filter((a: TaskAttachment) =>
      stagedRemovalIds.has(a.id),
    );

    await Promise.all(
      toRemove.map((a: TaskAttachment) => deleteAttachment(a.id)),
    );

    task.taskAttachments = task.taskAttachments.filter(
      (a: TaskAttachment) => !stagedRemovalIds.has(a.id),
    );
    stagedRemovalIds = new Set();
  }

  async function discardStagedRemovals(): Promise<void> {
    const staleTempUploads = task.taskAttachments.filter(
      (a: TaskAttachment) =>
        stagedRemovalIds.has(a.id) && a.taskItemId === null,
    );

    stagedRemovalIds = new Set();

    await Promise.allSettled(
      staleTempUploads.map((a: TaskAttachment) => deleteAttachment(a.id)),
    );
  }

  async function handleCancel(): Promise<void> {
    await discardStagedRemovals();
    onCancel();
  }

  onMount(async () => {
    parents = (await fetchParents()).filter(
      (p: ParentTask) => p.id !== task.id,
    );
  });

  async function resolveCategories(categories: Category[]): Promise<number[]> {
    const resolved = await Promise.all(
      categories.map(async (c) => {
        if (c.id > 0) return c.id;
        const created = await createCategory(c.name);
        return created.id;
      }),
    );
    return resolved;
  }

  function toggleVideo(e: any) {
    const video = e.currentTarget.querySelector("video");
    if (video.paused) {
      video.play();
    } else {
      video.pause();
    }
  }
</script>

{#if error}
  <ErrorPopup {error} />
{/if}
<form
  class="flex flex-col gap-6"
  onsubmit={async (e) => {
    task.title = task.title.trim();
    e.preventDefault();
    submitted = true;
    if (startDateChanged) {
      startDateTime.validate(startDateInput, startTimeInput);
    }
    if (startTimeChanged) {
      startDateTime.validate(startDateInput, startTimeInput);
    }
    if (dueDateChanged) {
      dueDateTime.validate(dueDateInput, dueTimeInput);
      startDateTime.validate(startDateInput, startTimeInput);
    }
    if (dueTimeChanged) {
      dueDateTime.validate(dueDateInput, dueTimeInput);
      startDateTime.validate(startDateInput, startTimeInput);
    }

    const form = e.currentTarget as HTMLFormElement;
    form.checkValidity();
    if (!form.checkValidity()) return;

    let resolvedCategoryIds: number[];
    try {
      resolvedCategoryIds = await resolveCategories(selectedCategories);
      task.categoryIds = resolvedCategoryIds;
    } catch (err) {
      console.error("Error creating categories:", err);
      error = "Failed to create one or more categories. Please try again.";
      return;
    }

    try {
      await commitStagedRemovals();
    } catch (err) {
      console.error("Error removing attachments:", err);
      error = "Failed to remove one or more attachments. Please try again.";
      return;
    }

    const payload = initialTask ? diffObjects(task, initialTask) : task;
    delete payload.categories;
    error = await onSubmit(payload as TaskForm);
  }}
  novalidate
>
  <div class="floating relative">
    <input
      id="taskTitle"
      type="text"
      class="input-field peer {submitted ? 'submitted' : ''}"
      bind:value={task.title}
      required
      placeholder="Task Name"
      pattern={"[^\\s].+[^\\s]"}
      maxlength="128"
      onblur={() => (task.title = task.title.trim())}
    />
    <label class="input-label" for="taskTitle">
      Title (3-128 characters) {task.title.length > 0
        ? "(" + task.title.length + " / 128)"
        : ""}
    </label>
    <p class="input-invalid">
      Title is required and must be between 3 and 128 characters long
    </p>
  </div>

  <div class="floating relative">
    <textarea
      use:autogrow
      id="taskDescription"
      class="text-area-field peer {submitted ? 'submitted' : ''}"
      bind:value={task.description}
      rows="1"
      placeholder="Description"
      maxlength="2048"
    ></textarea>
    <label class="input-label" for="taskDescription">
      Description (0-2048 characters) {task.description.length > 0
        ? "(" + task.description.length + " / 2048)"
        : ""}
    </label>
    <p class="input-invalid">Description must be 2048 characters or less</p>
  </div>

  <div
    class="flex flex-col lg:flex-row items-center justify-center w-full gap-4"
  >
    <div class="flex flex-row gap-4 w-full">
      <div class="relative flex-1">
        <input
          id="startDate"
          type="date"
          class="input-field peer {submitted ? 'submitted' : ''}"
          bind:value={task.startDate}
          bind:this={startDateInput}
          onchange={() =>
            startDateTime.validate(startDateInput, startTimeInput)}
        />
        <label class="input-label" for="startDate">Start Date</label>
        <p class="input-invalid">
          {startDateTime.startDateError ||
            `Invalid start date, date must be formatted ${getLocalisedDateFormat()}`}
        </p>
      </div>

      <div class="relative flex-1">
        <input
          id="startTime"
          type="time"
          class="input-field peer {submitted ? 'submitted' : ''}"
          disabled={!task.startDate}
          bind:value={task.startTime}
          bind:this={startTimeInput}
          onchange={() =>
            startDateTime.validate(startDateInput, startTimeInput)}
        />
        <label class="input-label" for="startTime">Start Time</label>
        <p class="input-invalid">
          {startDateTime.startTimeError ||
            "Invalid start time, time must be formatted HH:MM am/pm"}
        </p>
      </div>
    </div>

    <div class="flex flex-row gap-4 w-full">
      <div class="floating relative flex-1">
        <input
          id="dueDate"
          type="date"
          class="input-field peer {submitted ? 'submitted' : ''}"
          required
          bind:value={task.dueDate}
          bind:this={dueDateInput}
          onchange={() => dueDateTime.validate(dueDateInput, dueTimeInput)}
        />
        <label class="input-label" for="dueDate">Due Date</label>
        <p class="input-invalid">
          {dueDateTime.dueDateError ||
            `Invalid due date, date must be formatted ${getLocalisedDateFormat()}`}
        </p>
      </div>

      <div class="relative flex-1">
        <input
          id="taskDueTime"
          type="time"
          class="input-field peer {submitted ? 'submitted' : ''}"
          disabled={!task.dueDate}
          bind:value={task.dueTime}
          bind:this={dueTimeInput}
          onchange={() => dueDateTime.validate(dueDateInput, dueTimeInput)}
        />
        <label class="input-label" for="taskDueTime">Due Time</label>
        <p class="input-invalid">
          {dueDateTime.dueTimeError ||
            "Invalid due time, time must be formatted HH:MM am/pm"}
        </p>
      </div>
    </div>
  </div>

  <Combobox
    items={parents}
    bind:value={task.parentTaskId}
    getId={(p) => p.id}
    getLabel={(p) => p.title}
    label="Parent Task"
    placeholder="Search tasks..."
    id="taskParent"
  />

  <div
    class="rounded-lg border border-surface-alt p-4 max-h-128 flex flex-col gap-2"
  >
    <p class="text-sm font-medium">Categories</p>
    <CategoryList bind:categories={selectedCategories} />
  </div>

  <div class="rounded-lg border border-surface-alt p-4 flex flex-col gap-2">
    <p class="text-sm font-medium">
      Status <span class="text-red-500">*</span>
    </p>
    <div class="inline-flex gap-2 flex-wrap w-full justify-center">
      {#each Object.keys(TaskStatus).filter( (k) => isNaN(Number(k)), ) as statusKey}
        {@const value = TaskStatus[statusKey as keyof typeof TaskStatus]}
        <button
          type="button"
          class="secondary-button {value === task.itemStatus
            ? 'text-white'
            : 'bg-surface-alt! hover:bg-surface-alt-hover!'} border"
          style="background-color: {colourForStatus(
            value,
          )}; border-color: {colourForStatus(value)}"
          onclick={() => (task.itemStatus = value)}
        >
          <div class="inline-flex gap-2 items-center">
            <TaskIcon
              status={value}
              colour={task.itemStatus === value ? "white" : undefined}
            />
            {statusLabel(statusKey)}
          </div>
        </button>
      {/each}
    </div>
  </div>

  <Visibility bind:visibility={task.visibility} />

  <!-- attachments -->
  <div class="rounded-lg border border-surface-alt p-4">
    <div
      class="flex flex-col sm:flex-row gap-2 items-center text-center sm:text-left justify-center sm:justify-between w-full"
    >
      <div>
        <p class="text-sm font-medium">Attachments</p>
        <p class="text-xs text-text-muted">Add up to 5 images or videos</p>
      </div>

      <button
        type="button"
        class="secondary-button flex gap-2 items-center"
        onclick={openAttachmentModal}
        disabled={visibleAttachments.length >= 5 || cancelling}
      >
        <Pluscircle />
        Add Attachments
      </button>
    </div>

    {#if visibleAttachments.length > 0}
      <div class="mt-3 grid grid-cols-2 md:grid-cols-3 gap-2">
        {#each visibleAttachments as attachment (attachment.id)}
          <div
            class="relative aspect-square overflow-hidden rounded-lg border border-surface-alt"
          >
            {#if attachment.contentType?.startsWith("video/")}
              <button
                type="button"
                aria-label="Toggle play"
                class="contents"
                onclick={toggleVideo}
              >
                <video
                  src={`${taskAttachmentUrl(attachment.id)}#t=0.1`}
                  muted
                  playsinline
                  autoplay
                  preload="metadata"
                  class="h-full w-full object-cover"
                ></video>
              </button>
            {:else}
              <img
                src={taskAttachmentUrl(attachment.id)}
                alt={attachment.originalFileName}
                loading="lazy"
                class="h-full w-full object-cover"
              />
            {/if}
            <button
              type="button"
              class="absolute top-1 right-1 group/remove icon-button hover:text-red-500 bg-surface-alt hover:bg-surface-alt-hover opacity-80"
              onclick={() => removeAttachment(attachment)}
              disabled={cancelling}
              aria-label="Remove attachment"
              use:tooltip={"Remove attachment"}
            >
              <Xicon size={1} />
            </button>
          </div>
        {/each}
      </div>
    {/if}
  </div>

  <hr class="text-surface-alt-hover" />
  <div class="flex flex-row items-center gap-4 w-full">
    <button type="button" class="secondary-button" onclick={handleCancel}>
      Cancel
    </button>
    <button type="submit" class="primary-button w-full">
      {editing ? "Update Task" : "Create Task"}
    </button>
  </div>
  {#if showConfirmModal}
    <ConfirmAttachmentsModal
      onUpload={uploadAttachments}
      onCancel={() => (showConfirmModal = false)}
      maxFiles={5 - visibleAttachments.length}
      autoOpenPicker={true}
    />
  {/if}
</form>
