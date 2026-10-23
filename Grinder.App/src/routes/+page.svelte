<script>
  import { resolve } from "$app/paths";
  import Broadcast from "$lib/components/svgs/broadcast.svelte";
  import Clip from "$lib/components/svgs/clips/clip.svelte";
  import { theme } from "$lib/stores/theme";
  import ThemeToggle from "$lib/components/svgs/theme.svelte";
  import { tooltip } from "$lib/utils/tooltip";
</script>

<div class="min-h-screen bg-surface">
  <button
    onclick={theme.toggle}
    class="icon-button absolute top-4 right-4"
    use:tooltip={$theme === "dark" ? "Light Mode" : "Dark Mode"}
  >
    <ThemeToggle size={1.5} dark={$theme === "dark"} />
  </button>
  <!-- Hero -->
  <section class="bg-surface-alt">
    <div class="max-w-7xl mx-auto px-4 py-20 md:py-32">
      <div class="grid md:grid-cols-2 gap-12 items-center">
        <div class="text-left">
          <p class="mb-4 text-lg font-medium uppercase tracking-widest">
            Grinder
          </p>
          <h1 class="max-w-2xl text-4xl md:text-5xl text-left">
            Never grind alone again.
          </h1>
          <p class="mt-6 max-w text-lg leading-relaxed text-text-muted">
            Share tasks, cheer on your friends, and make accountability actually
            fun!
          </p>
          <div class="mt-10 flex items-center justify-start gap-4">
            <a class="primary-button" href={resolve("/register")}> Register </a>
            <a class="primary-button" href={resolve("/login")}> Login </a>
          </div>
        </div>
        <!-- Card section -->
        <div class="hidden md:flex items-center justify-center">
          <div class="relative h-72 w-72">
            <div class="absolute inset-0 rounded-3xl bg-brand/5 rotate-6"></div>
            <div
              class="absolute inset-0 rounded-3xl bg-surface-alt border border-surface-alt-hover shadow-sm p-8 flex flex-col gap-4"
            >
              <div class="flex items-center gap-3">
                <div
                  class="h-5 w-5 rounded-full border-2 border-brand bg-brand text-surface text-xs flex items-center justify-center"
                >
                  &#10003;
                </div>
                <span class="text-sm text-text-muted line-through">
                  Design landing page
                </span>
              </div>
              <div class="flex items-center gap-3">
                <div
                  class="h-5 w-5 rounded-full border-2 border-brand bg-brand text-surface text-xs flex items-center justify-center"
                >
                  &#10003;
                </div>
                <span class="text-sm text-text-muted line-through">
                  Set up project
                </span>
              </div>
              <div class="flex items-center gap-3">
                <div
                  class="h-5 w-5 rounded-full border-2 border-amber-400 bg-amber-50"
                ></div>
                <span class="text-sm text-stone-400">
                  Build feature cards
                </span>
              </div>
              <div class="flex items-center gap-3">
                <div
                  class="h-5 w-5 rounded-full border-2 border-surface-alt-hover"
                ></div>
                <span class="text-sm text-stone-400"> Write tests </span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </section>
  <!-- Features -->
  <main class="max-w-7xl mx-auto px-4">
    <section class="py-24">
      <h2 class="text-left">Why Grinder?</h2>
      <p class="text-sm text-text-muted mt-2 mb-16!">
        Productivity, through encouragement~
      </p>

      <div class="flex flex-col gap-12 max-w-2xl">
        <div class="flex items-start gap-6">
          <div class="p-4 shrink-0 rounded-lg bg-surface-alt text-brand">
            <Clip size={2} />
          </div>
          <div>
            <h5 class="text-left">Share tasks and clips</h5>
            <p class="mt-1 text-sm leading-relaxed text-text-muted">
              Quick videos or pictures of you doing the work, so friends can
              cheer you on.
            </p>
          </div>
        </div>
        <div class="flex items-start gap-6">
          <div class="p-4 shrink-0 rounded-lg bg-surface-alt text-brand">
            <Broadcast size={2} />
          </div>
          <div>
            <h5 class="text-left">Grind together</h5>
            <p class="mt-1 text-sm leading-relaxed text-text-muted">
              Go live, share calendars, and earn badges.
            </p>
          </div>
        </div>
      </div>
    </section>
    <!-- Bottom banner -->
    <section class="pb-16">
      <div
        class="rounded-2xl bg-surface-alt px-8 py-12 md:px-16 flex flex-col md:flex-row items-start md:items-center justify-between gap-6"
      >
        <div>
          <h2 class="text-left">Ready to start grinding?</h2>
          <p class="mt-2 text-sm text-text-muted">
            Built for channeling productivity.
          </p>
        </div>
        <a class="primary-button" href={resolve("/register")}> Get started </a>
      </div>
    </section>
  </main>
  <footer class="border-t border-surface-alt">
    <div
      class="mx-auto flex max-w-7xl flex-col items-center gap-3 px-4 py-6 text-sm text-text-muted md:flex-row md:justify-between"
    >
      <span>&copy; {new Date().getFullYear()} Grinder</span>
      <div class="flex items-center gap-4">
        <a class="underline hover:text-text" href={resolve("/terms")}
          >Terms of Service</a
        >
        <a class="underline hover:text-text" href={resolve("/privacy")}
          >Privacy Policy</a
        >
        <a class="underline hover:text-text" href={resolve("/cookies")}
          >Cookie Policy</a
        >
        <a class="underline hover:text-text" href={resolve("/attributions")}
          >Attributions</a
        >
      </div>
    </div>
  </footer>
</div>
