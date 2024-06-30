<template>
  <div class="icon" :class="[`icon--${name}`, `icon--${size}`, { 'has-align-fix': fixAlign }]">
    <svg class="icon__svg">
      <use xmlns:xlink="http://www.w3.org/1999/xlink" :xlink:href="'#icon--' + name"></use>
    </svg>
  </div>
</template>

<script>
export default {
  props: {
    name: {},
    size: {
      default: 'normal',
    },
    modifier: {
      default: null,
    },
    fixAlign: {
      default: true,
    },
  },
}
</script>

<style scoped>

.icon {
  position: relative;
  display: inline-block;
  vertical-align: middle;
  width: 0.8rem;
  height: 0.8rem;
  margin: 0 0.3rem;
  top: -0.05rem;
  fill: currentColor;
}
.icon__svg {
  display: inline-block;
  vertical-align: top;
  width: 100%;
  height: 100%;
}
.icon:first-child {
  margin-left: 0;
}
.icon:last-child {
  margin-right: 0;
}
body > svg path, .icon use > svg path, symbol path, body > svg rect, .icon use > svg rect, symbol rect, body > svg circle, .icon use > svg circle, symbol circle, body > svg g, .icon use > svg g, symbol g {
  fill: currentColor;
  stroke: none;
}
body > svg *[d="M0 0h24v24H0z"], .icon use > svg *[d="M0 0h24v24H0z"], symbol *[d="M0 0h24v24H0z"] {
  display: none;
}
</style>
