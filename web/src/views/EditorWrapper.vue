<template>
  <div class="content">
    <div class="nav">
      {{ time }}
      <a href="#">{{ date }}</a>
      <a v-if="date != today" :href="'/view?date=' + today">Today</a>
      <a v-if="date == today" href="/todo">Todo</a>
      <a href="content">
        <Icon :icon="icons.tableOfContents" />
        <Icon v-if="loading" icon="line-md:loading-alt-loop" />
      </a>
    </div>
    <div class="editor">
      <el-tiptap :content="content" :extensions="extensions" @onUpdate="debouncedOnUpdate" @onInit="onInit"></el-tiptap>
    </div>
  </div>
</template>
<script>
import { debounce } from 'lodash';
import moment from 'moment';
import { Icon } from '@iconify/vue2';
import tableOfContents from '@iconify/icons-mdi/table-of-contents';
import { createExtensions } from '@/editorExt.js';
import codemirror from 'codemirror';
import 'codemirror/lib/codemirror.css'; // import base style
import 'codemirror/mode/xml/xml.js'; // language
import 'codemirror/addon/selection/active-line.js'; // require active-line.js
import 'codemirror/addon/edit/closetag.js'; // autoCloseTags

export default {
  components: {
    Icon,
  },
  props: {
    date: String
  },
  data() {
    return {
      now: moment(),
      loading: true,
      timeFormat: 'h:mm:ss a',
      last_note_json: null,
      content: null,
      icons: {
        tableOfContents,
      },
      extensions: createExtensions()
    };
  },
  created() {

    // eslint-disable-next-line no-unused-vars
    var interval = setInterval(() => this.now = moment(), 1000);
    // this.date = this.$route.query.date;
  },
  computed: {
    today() {
      return this.now.format('YYYYMMDD');
    },
    time() {
      return this.now.format(this.timeFormat);
    }

  },
  methods: {
    onUpdate(output, options) {
      const { getJSON, getHTML } = options;
      console.log(this.date);
      this.json = getJSON();
      let app = this;
      this.axios
        .put(
          `/api/diary/${this.date}`,
          {
            noteId: this.date,
            note: JSON.stringify(this.json)
          },
        )
        .then(function (response) {
          console.log(response);
          app.loading = false;
        })
        .catch(function (error) {
          app.loading = false;
          if (error.response.status == 401 ) {
            app.$router.push({ name: 'login' });
          }
          console.log(error);
        });
    },
    debouncedOnUpdate: debounce(function (output, options) {
      this.onUpdate(output, options);
    }, 500),
    onInit({ editor }) {
      let app = this;
      // this.date = this.$route.query.date;
      let date = this.date;
      app.loading = true;
      this.axios
        .get(`/api/diary/${date}`)
        .then(function (response) {
          // handle success
          app.loading = false;
          let last_note = response.data;
          if (last_note) {
            let last_note_json = JSON.parse(last_note.note);
            // set content should trigger OnUpdate?
            console.log(last_note_json);
            app.last_note_json = last_note_json;
            editor.setContent(last_note_json);
          }
        })
        .catch(function (error) {
          // handle error
          console.log(error);
        })
        .then(function () {
          // always executed
        });
    }
  }
};
</script>

<style>
.content {
  max-width: 65rem;
  margin: auto;
}

.nav {
  margin: 1em 1em 1rem 1em;
  display: flex;
  justify-content: space-between;
}

pre code {
  font-family: "Fira Code", Courier, Monaco, monospace;
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
