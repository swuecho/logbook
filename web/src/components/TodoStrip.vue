<template>
  <section class="todo-strip">
    <div class="todo-strip__header">
      <div class="todo-strip__title">
        <span class="todo-strip__label">Todos</span>
        <span v-if="totalCount" class="todo-strip__count">{{ totalCount }}</span>
      </div>
      <div class="todo-strip__actions">
        <button type="button" class="todo-strip__filter" @click="toggleCompleted">
          {{ showCompleted ? 'Hide done' : 'Show done' }}
        </button>
        <button type="button" class="todo-strip__toggle" @click="toggleExpanded">
          {{ expanded ? 'Close' : 'Open' }}
        </button>
      </div>
    </div>
    <div class="todo-strip__body">
      <div v-if="isLoading" class="todo-strip__empty">Loading todos...</div>
      <div v-else-if="errorMessage" class="todo-strip__empty">{{ errorMessage }}</div>
      <ul v-else-if="previewItems.length" class="todo-strip__list">
        <li v-for="(item, index) in previewItems" :key="itemKey(item, index)" class="todo-strip__item">
          <span class="todo-strip__check" :class="{ 'is-done': item.done }"></span>
          <span class="todo-strip__text" :class="{ 'is-done': item.done }">
            {{ item.text || 'Untitled task' }}
          </span>
          <a v-if="item.noteId" class="todo-strip__note" :href="`/view?date=${item.noteId}`">
            {{ item.noteId }}
          </a>
        </li>
      </ul>
      <div v-else class="todo-strip__empty">{{ emptyMessage }}</div>
    </div>
    <div v-if="remainingCount > 0" class="todo-strip__more">{{ remainingCount }} more...</div>
    <transition name="todo-strip-expand">
      <div v-show="expanded" class="todo-strip__expanded">
        <todo-editor
          v-if="todoContent"
          :content="todoContent"
          :extensions="extensions"
          @init="onInit"
        />
        <div v-else class="todo-strip__empty">Loading todos...</div>
      </div>
    </transition>
  </section>
</template>

<script setup>
import { ref, computed, watch } from 'vue';
import { useQuery } from '@tanstack/vue-query';
import { fetchTodoContent } from '@/services/todo';
import { getApiErrorMessage } from '@/services/apiError';
import TodoEditor from '@/components/TodoEditor';
import { createExtensions } from '@/editorExt.js';

const expanded = ref(false);
const showCompleted = ref(false);
const extensions = createExtensions();
const editorRef = ref(null);

const { data, isLoading, isError, error } = useQuery({
  queryKey: ['todoContent'],
  queryFn: fetchTodoContent,
});

const todoContent = computed(() => normalizeTodoDoc(data.value));
const allItems = computed(() => extractTodoItems(todoContent.value));
const openItems = computed(() => allItems.value.filter((item) => !item.done));
const previewItems = computed(() => {
  if (showCompleted.value) {
    return allItems.value.slice(0, 3);
  }
  return openItems.value.slice(0, 3);
});
const remainingCount = computed(() => {
  const source = showCompleted.value ? allItems.value : openItems.value;
  return Math.max(source.length - previewItems.value.length, 0);
});
const totalCount = computed(() => openItems.value.length);
const errorMessage = computed(() => (
  isError.value ? getApiErrorMessage(error.value, 'Could not load todos.') : ''
));
const emptyMessage = computed(() => (
  allItems.value.length > 0 && !showCompleted.value ? 'All caught up.' : 'No todos yet.'
));

watch(todoContent, (value) => {
  if (editorRef.value && value) {
    editorRef.value.setContent(value);
  }
});

function toggleExpanded() {
  expanded.value = !expanded.value;
}

function toggleCompleted() {
  showCompleted.value = !showCompleted.value;
}

function onInit({ editor }) {
  editorRef.value = editor;
  if (todoContent.value) {
    editor.setContent(todoContent.value);
  }
}

function itemKey(item, index) {
  if (item.noteId) {
    return `${item.noteId}-${index}`;
  }
  return `todo-${index}`;
}

function normalizeTodoDoc(payload) {
  if (!payload) return null;
  if (typeof payload === 'string') {
    try {
      return JSON.parse(payload);
    } catch (parseError) {
      return null;
    }
  }
  return payload;
}

function extractTodoItems(doc) {
  if (!doc) return [];
  const items = [];
  let currentNoteId = null;

  const walk = (node) => {
    if (!node) return;
    if (node.type === 'heading') {
      const headingText = flattenText(node).trim();
      if (headingText) currentNoteId = headingText;
    }

    if (node.type === 'todo_item') {
      const text = flattenText(node).trim();
      items.push({
        text,
        done: Boolean(node.attrs && node.attrs.done),
        noteId: currentNoteId,
      });
      return;
    }

    if (Array.isArray(node.content)) {
      node.content.forEach(walk);
    }
  };

  walk(doc);
  return items;
}

function flattenText(node) {
  if (!node) return '';
  if (node.type === 'text') return node.text || '';
  if (Array.isArray(node)) return node.map(flattenText).join('');
  if (Array.isArray(node.content)) {
    return node.content.map(flattenText).join('');
  }
  return '';
}
</script>

<style scoped>
.todo-strip {
  border: 1px solid #e6e6e6;
  border-radius: 10px;
  padding: 0.75rem 0.9rem;
  background: #fafafa;
}

.todo-strip__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
}

.todo-strip__title {
  display: flex;
  align-items: baseline;
  gap: 0.4rem;
  font-weight: 600;
  color: #333;
}

.todo-strip__count {
  font-size: 0.85rem;
  color: #777;
}

.todo-strip__toggle {
  border: 1px solid #d9d9d9;
  background: #fff;
  padding: 0.25rem 0.7rem;
  border-radius: 999px;
  font-size: 0.8rem;
  cursor: pointer;
}

.todo-strip__actions {
  display: flex;
  align-items: center;
  gap: 0.4rem;
}

.todo-strip__filter {
  border: 1px solid #d9d9d9;
  background: #f1f3f5;
  padding: 0.25rem 0.6rem;
  border-radius: 999px;
  font-size: 0.8rem;
  cursor: pointer;
}

.todo-strip__body {
  margin-top: 0.6rem;
}

.todo-strip__list {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}

.todo-strip__item {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.todo-strip__check {
  width: 0.75rem;
  height: 0.75rem;
  border: 1.5px solid #9ca3af;
  border-radius: 3px;
  flex: 0 0 auto;
}

.todo-strip__check.is-done {
  background: #cdebd2;
  border-color: #5c9c63;
}

.todo-strip__text {
  flex: 1;
  color: #333;
  font-size: 0.95rem;
}

.todo-strip__text.is-done {
  text-decoration: line-through;
  color: #7a7a7a;
}

.todo-strip__note {
  font-size: 0.75rem;
  color: #4a68a6;
  text-decoration: none;
  border: 1px solid #d9e1f2;
  padding: 0.1rem 0.35rem;
  border-radius: 6px;
  background: #f5f7fb;
  flex: 0 0 auto;
}

.todo-strip__more {
  margin-top: 0.4rem;
  font-size: 0.8rem;
  color: #666;
}

.todo-strip__expanded {
  margin-top: 0.75rem;
  border-top: 1px dashed #ddd;
  padding-top: 0.75rem;
  max-height: 320px;
  overflow: auto;
}

.todo-strip__empty {
  font-size: 0.9rem;
  color: #777;
}

.todo-strip-expand-enter-active,
.todo-strip-expand-leave-active {
  transition: opacity 0.2s ease, transform 0.2s ease;
}

.todo-strip-expand-enter,
.todo-strip-expand-leave-to {
  opacity: 0;
  transform: translateY(-4px);
}

@media (max-width: 768px) {
  .todo-strip__item {
    align-items: flex-start;
    gap: 0.4rem;
  }

  .todo-strip__note {
    font-size: 0.7rem;
  }
}
</style>
