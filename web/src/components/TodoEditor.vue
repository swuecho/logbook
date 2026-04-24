<template>
        <div class="todo">
                <el-tiptap
                        output="json"
                        :content="content"
                        :extensions="extensions"
                        :readonly="true"
                        :enable-char-count="false"
                        @onCreate="$emit('init', $event)"
                />
        </div>
</template>

<script setup>
defineProps({
        content: {
                type: [String, Object],
                required: true
        },
        extensions: {
                type: Array,
                required: true
        }
});
</script>

<style scoped>
.todo :deep(.el-tiptap-editor__content) {
        border: 1px solid var(--lb-border, #ebeef5) !important;
        border-radius: var(--lb-radius-md, 8px) !important;
        background: #fff !important;
        padding: 0.4rem 0.5rem !important;
}

.todo :deep(.el-tiptap-editor__menu-bar) {
        display: none;
}

.todo :deep(.el-tiptap-editor__footer) {
        display: none;
}


.todo :deep(pre code) {
        font-family: var(--lb-font-mono, "Fira Code", Courier, Monaco, monospace);
}
</style>
