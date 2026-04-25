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
        border-radius: var(--lb-radius-lg, 10px) !important;
        background: #fff !important;
        padding: 0.5rem 0.65rem !important;
}

.todo :deep(.el-tiptap-editor__menu-bar) {
        display: none;
}

.todo :deep(.el-tiptap-editor__footer) {
        display: none;
}

.todo :deep(.ProseMirror) {
        line-height: 1.65;
}

.todo :deep(pre code) {
        font-family: var(--lb-font-mono, "Fira Code", Courier, Monaco, monospace);
}
</style>
