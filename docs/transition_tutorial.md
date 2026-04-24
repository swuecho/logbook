## How `<transition-group>` Layout Shifts Work (and the Fix)

### The Problem

Vue's `<transition-group>` animates list items entering and leaving the DOM. The key behavior:

```
Time 0 (before slide):    [Item A] [Item B] [Item C]      → 3 items in flow
Time 1 (during transition): [Item A leaving] [Item B leaving] [Item C leaving]
                            [Item D entering] [Item E entering] [Item F entering]
                                                              → 6 items in flow!
Time 2 (after slide):     [Item D] [Item E] [Item F]      → 3 items in flow
```

During the transition, **both old and new items exist in the DOM simultaneously**. Since all items are in the normal document flow (`position: static`), the container's height is determined by all 6 items, causing it to temporarily double in height.

### The Fix: 3 CSS Properties

**1. `position: relative` on the list container**

```css
.todo-strip__list {
  position: relative;
}
```

This establishes a **positioning context**. When a child uses `position: absolute`, it positions itself relative to the nearest ancestor with `position: relative`.

**2. `position: absolute` on leaving items**

```css
.todo-strip-slide-leave-active {
  position: absolute;
  width: 100%;
}
```

This is the core fix. When an item starts its leave animation, it's taken **out of the normal document flow**. It no longer contributes to the container's height calculation. The item still visually animates (fading/sliding out), but from the layout engine's perspective, it doesn't exist.

`width: 100%` is needed because absolute-positioned elements shrink-wrap by default — without it, the items would collapse to their content width during exit.

**3. `overflow: hidden` on the body container**

```css
.todo-strip__body {
  overflow: hidden;
}
```

Since leaving items are now absolutely positioned, they might visually overflow their container (e.g., when `transform: translateY(6px)` pushes them down). `overflow: hidden` clips them so they disappear cleanly at the boundary.

### Visual Summary

```
Without fix:
┌─────────────────────┐
│ Item A (leaving)    │ ← still in flow, adds height
│ Item B (leaving)    │
│ Item C (leaving)    │
│ Item D (entering)   │
│ Item E (entering)   │
│ Item F (entering)   │
└─────────────────────┘  ← container height = 6 items
[DiaryEditor pushed down]

With fix:
┌─────────────────────┐
│ Item D (entering)   │
│ Item E (entering)   │
│ Item F (entering)   │
└─────────────────────┘  ← container height = 3 items
  Item A (absolute, fading out — not in flow)
  Item B (absolute, fading out — not in flow)
  Item C (absolute, fading out — not in flow)
[DiaryEditor stays put]
```

### When to Use This Pattern

Any time you have a `<transition-group>` where:
- Items rotate/swap (slideshows, carousels)
- Items are added/removed and you don't want the container height to jump

This is a standard Vue transition pattern — the official Vue docs also recommend `position: absolute` on leave-active for list transitions.
