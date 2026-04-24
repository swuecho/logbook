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

<style>
.todo .el-tiptap-editor__content {
        border: 1px solid var(--lb-border, #ebeef5) !important;
        border-radius: 14px !important;
        background: rgba(255, 255, 255, 0.96) !important;
        padding: 0.4rem 0.5rem !important;
}

.todo .el-tiptap-editor__menu-bar {
        display: none;
}

.todo .el-tiptap-editor__footer {
        display: none;
}


pre code {
        font-family: var(--lb-font-mono, "Fira Code", Courier, Monaco, monospace);
}
</style>
