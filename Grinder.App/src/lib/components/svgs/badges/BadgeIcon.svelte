<script lang="ts">
    import Grinder from './grinder.svelte';
    import Broom from './broom.svelte';
    import Snail from './snail.svelte';
    import Stars from './stars.svelte';
    import CameraBadge from './camera-badge.svelte';
    import Divide from './divide.svelte';
    import Notebook from './notebook.svelte';
    import FriendsIcon from './friends-icon.svelte';

    let {
        icon,
        size = 1.5,
    }: {
        icon: string;
        size?: number;
    } = $props();

    const icons: Record<string, any> = {
        grinder: Grinder,
        broom: Broom,
        snail: Snail,
        stars: Stars,
        camera: CameraBadge,
        divide: Divide,
        notebook: Notebook,
        friends: FriendsIcon,
    };

    let IconComponent = $derived(icons[icon]);
</script>

  {#if IconComponent}
    <IconComponent {size} />
  {/if}
