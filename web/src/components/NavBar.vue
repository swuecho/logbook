<template>
        <div class="nav">
                <a v-if="date != today" :href="'/view?date=' + today">Diary</a>
                <a href="content">
                        <Icon :icon="icons.tableOfContents" />
                        <Icon v-if="loading" icon="line-md:loading-alt-loop" />
                </a>
        </div>
</template>

<script setup>
import { ref } from 'vue';
import { Icon } from '@iconify/vue2';
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
        display: inline-block;
        text-decoration: none;
        border-radius: 5%;
}

/* Change the link color on hover */
.nav a:hover {
        background-color: rgb(223, 214, 214);
        color: white;
}
</style>