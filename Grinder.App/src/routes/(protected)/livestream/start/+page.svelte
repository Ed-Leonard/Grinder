<script lang="ts">
  import { onMount } from "svelte";
  import { goto } from "$app/navigation";
  import BasePage from "$lib/components/utils/BasePage.svelte";
  import { fetchTasks, flattenTasks } from "$lib/api/tasks";
  import { TaskVisibility, type Task } from "$lib/types";
  import { attachTrack } from "$lib/utils/attach-track";
  import { broadcast } from "$lib/stores/broadcast.svelte";
  import Visibility from "$lib/components/FormFields/Visibility.svelte";
  import Combobox from "$lib/components/FormFields/Combobox.svelte";
  import Error from "$lib/components/utils/Error.svelte";
  import Select from "$lib/components/FormFields/Select.svelte";
  import { resolve } from "$app/paths";
  import { user } from "$lib/stores/auth";

  let tasks = $state<Task[]>([]);
  let loading = $state(false);
  let submitted = $state(false);

  let selectedTask = $derived(
    tasks.find((task) => task.id === broadcast.taskId),
  );

  let taskIsFriendsOnly = $derived(
    selectedTask?.visibility === TaskVisibility.FriendsOnly,
  );

  $effect(() => {
    if (taskIsFriendsOnly) {
      broadcast.visibility = TaskVisibility.FriendsOnly;
    }
  });

  async function goLive(e: Event) {
    e.preventDefault();
    submitted = true;
    loading = true;

    const form = e.currentTarget as HTMLFormElement;

    const formValid = form.checkValidity();

    if (!formValid) {
      loading = false;
      return;
    }

    if (!broadcast.title || !selectedTask) {
      broadcast.error = "Title and task are required.";
      loading = false;
      return;
    }

    if (taskIsFriendsOnly && broadcast.visibility === TaskVisibility.Public) {
      broadcast.error =
        "Cannot start a public live stream with a friend's only task";
      loading = false;
      return;
    }

    const success = await broadcast.goLive({
      title: broadcast.title,
      taskId: selectedTask.id,
      taskTitle: selectedTask.title,
      visibility: broadcast.visibility,
    });

    loading = false;
    if (success) {
      await goto(resolve("/livestream/manage"));
    }
  }

  onMount(() => {
    fetchTasks()
      .then((result) => {
        tasks = flattenTasks(result).filter(
          (task) =>
            task.visibility === TaskVisibility.FriendsOnly ||
            task.visibility === TaskVisibility.Public,
        );
      })
      .catch(() => (broadcast.error = "Could not load your tasks."))
      .finally(() => (loading = false));

    broadcast.requestDevices();

    return () => {
      if (!broadcast.connected) {
        broadcast.disposeAll();
      }
    };
  });
</script>

<BasePage maxWidth="max-w-4xl" class="md:p-8">
  <section class="card flex flex-col gap-4">
    <h1 class="text-left">Start Stream</h1>

    {#if broadcast.error}
      <Error error={broadcast.error} />
    {/if}

    <form class="flex flex-col gap-4" novalidate onsubmit={goLive}>
      <div class="floating relative">
        <input
          id="broadcastTitle"
          type="text"
          class="input-field peer {submitted ? 'submitted' : ''}"
          bind:value={broadcast.title}
          required
          placeholder="Task Name"
		  pattern={"[^\\s](.*[^\\s])?"}
          maxlength="128"
          onblur={() => (broadcast.title = broadcast.title.trim())}
        />
        <label class="input-label" for="taskTitle">
          Title (1-128 characters) {broadcast.title.length > 0
            ? "(" + broadcast.title.length + " / 128)"
            : ""}
        </label>
        <p class="input-invalid">
          Title is required and must be between 1 and 128 characters long
        </p>
      </div>

      <Combobox
        items={tasks}
        bind:value={broadcast.taskId}
        getId={(t) => t.id}
        getLabel={(t) => t.title}
        label="Connected Task"
        {submitted}
        required={true}
        getError={() => (submitted ? "Task is required" : "")}
        placeholder="Search tasks..."
        id="task"
      />

      <Visibility
        bind:visibility={broadcast.visibility}
        options={[TaskVisibility.FriendsOnly, TaskVisibility.Public]}
        disabledOptions={taskIsFriendsOnly ? [TaskVisibility.Public] : []}
      />

      {#if taskIsFriendsOnly}
        <p class="text-sm text-text-muted -mt-2">
          This task is friends-only, so your stream can only be shared with
          friends.
        </p>
      {/if}

      <div class="rounded-lg border border-surface-alt p-4 flex flex-col gap-4">
        <p class="text-sm font-medium w-fit">Camera & audio</p>

        <Select
          id="camerapicker"
          bind:value={broadcast.selectedCameraId}
          items={broadcast.cameras}
          getValue={(device) => device.deviceId}
          getLabel={(device) => device.label}
          onchange={() => broadcast.startPreview()}
          label="Camera"
          emptyLabel="No camera selected"
          required={false}
          {submitted}
          {loading}
        />
        <Select
          id="audiopicker"
          bind:value={broadcast.selectedMicrophoneId}
          items={broadcast.microphones}
          getValue={(device) => device.deviceId}
          getLabel={(device) => device.label}
          onchange={() => broadcast.enableAudioPreview()}
          label="Audio"
          emptyLabel="No audio selected"
          {submitted}
          {loading}
        />
      </div>

      <div
        class="rounded-lg border border-surface-alt p-4 flex flex-col gap-4 w-full"
      >
        <p class="text-sm font-medium">Stream preview</p>

        {#if broadcast.previewTrack}
          <div class="aspect-video w-full overflow-hidden rounded-lg bg-black">
            <!-- mirror preview to avoid jumpscare -->
            <video
              use:attachTrack={broadcast.previewTrack}
              autoplay
              muted
              playsinline
              class="h-full w-full object-contain scale-x-[-1]"
            ></video>
          </div>
          <div
            class="text-sm text-text-muted flex flex-col-reverse sm:flex-row sm:items-center sm:justify-between gap-2 sm:gap-4 my-2"
          >
            <span>
              {broadcast.previewTrack
                ? "Camera ready."
                : "Grant access to start the preview."}
              {broadcast.previewAudio
                ? broadcast.micMuted
                  ? "Microphone muted."
                  : "Microphone ready."
                : ""}
            </span>
          </div>
        {/if}
        {#if broadcast.previewAudio}
          <div
            class="inline-flex items-center justify-center gap-2 h-16 -mt-2"
            aria-hidden="true"
          >
            {#each Array(16) as _, i}
              {@const threshold = i / 16}
              <div
                class="w-2 rounded-sm transition-colors {broadcast.audioLevel >
                threshold
                  ? i > 8
                    ? i > 10
                      ? 'bg-red-500'
                      : 'bg-yellow-500'
                    : 'bg-green-500'
                  : 'bg-surface-alt'}"
                style="height: {20 + i * 4}%"
              ></div>
            {/each}
          </div>
        {/if}
      </div>
      <div class="flex flex-row items-center gap-4 w-full">
        <button
          type="button"
          class="secondary-button"
          onclick={() => goto(resolve(`/profile/${$user?.id}`))}
        >
          Cancel
        </button>
        <button class="primary-button w-full" disabled={loading}>
          {loading ? "Starting Stream..." : "Start Stream"}
        </button>
      </div>
    </form>
  </section>
</BasePage>
