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
        border-bottom: 1px solid #ebeef5 !important;
        border-top: 1px solid #ebeef5 !important;
        border-radius: 5px !important;
}

.todo .el-tiptap-editor__menu-bar {
        display: none;
}

.todo .el-tiptap-editor__footer {
        display: none;
}


pre code {
        font-family: "Fira Code", Courier, Monaco, monospace;
}
</style>