<template>
        <div class="nav">
                <a v-if="date != today" href="/">Diary</a>
                <a href="content">
                        <Icon :icon="icons.tableOfContents" />
                        <Icon v-if="loading" icon="line-md:loading-alt-loop" />
                </a>
        </div>
</template>

<script setup>
import { ref } from 'vue';
import { Icon } from '@iconify/vue';
import tableOfContents from '@iconify/icons-mdi/table-of-contents';

defineProps({
        date: String,
        today: String,
        loading: Boolean
});

const icons = ref({ tableOfContents });

</script>

<style scoped>
.nav {
        margin: 1em 1em 1rem 1em;
        display: flex;
        justify-content: space-between;
}

.nav a {
        display: inline-flex;
        align-items: center;
        min-height: 2rem;
        padding: 0.25rem 0.35rem;
        color: var(--lb-text-muted);
        text-decoration: none;
        border-radius: var(--lb-radius-sm, 6px);
}

.nav a:hover {
        background-color: var(--lb-hover);
        color: var(--lb-text);
}
</style>
