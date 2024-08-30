<template>
  <el-container>
    <el-header style="text-align: right">
      <div @click="backHome">
        <Icon :icon="icons.homeIcon" height="28" />
      </div>
    </el-header>
    <el-main>
      <div class="content">
        <div v-for="(column, row_idx) in rows" :key="row_idx">
          <el-row>
            <el-col :span="12" v-for="(item, col_index) in column" :key="col_index">
              <el-card class="box-card">
                <div slot="header" class="clearfix">
                  <span style="float: left; padding: 0 0 5px 0">
                    <a :href="'/view?date=' + item.id">{{ item.id }}</a>
                  </span>
                </div>
                <vue-word-cloud style="
        height: 240px;
        width: 300px;
      " :words="item.note" :color="([, weight]) => weight > 10 ? 'DeepPink' : weight > 5 ? 'RoyalBlue' : 'Indigo'"
                  font-family="Roboto" />
                <div></div>
              </el-card>
            </el-col>
          </el-row>
        </div>
      </div>
    </el-main>
  </el-container>
</template>

<script lang="js">
import VueWordCloud from 'vuewordcloud';
import { Icon } from '@iconify/vue2';
import homeIcon from '@iconify/icons-material-symbols/home';
export default {
  components: {
    [VueWordCloud.name]: VueWordCloud,
    Icon,
  },
  data() {
    return {
      dates: [],
      cols: 2,
      content: {},
      icons: {
        homeIcon,
      },
    };
  },
  methods: {
    dict_to_lol(dict) {
      var lol = [];
      for (const [key, value] of Object.entries(JSON.parse(dict))) {
        lol.push([key, value])
      }
      // return JSON.stringify(lol);
      return lol;
    },
    backHome() {
      console.log("backhome")
      this.$router.push('/')
    }
  },
  computed: {
    rows: function () {
      let rows = [];
      let array = this.dates;
      var i,
        j,
        temparray,
        chunk = this.cols;
      for (i = 0, j = array.length; i < j; i += chunk) {
        temparray = array.slice(i, i + chunk);
        rows.push(temparray);
      }
      return rows;
    },
  },
  created() {
    let app = this;
    this.axios
      .get(`/api/diary`)
      .then(function (response) {
        // handle success
        let notes = response.data
        let changed = notes.map((note) => ({ id: note.noteId, note: app.dict_to_lol(note.note) })).filter((doc) => doc.note.length > 0);
        app.notes = changed;
        app.dates = app.notes
      })
      .catch(function (error) {
        // handle error
        console.log(error);
      })
      .then(function () {
        // always executed
      });
  },
};
</script>

<style scoped>
.xcontent {
  margin: 10% auto;
  text-align: center;
}

.el-row {
  margin-bottom: 20px;
}

.box-card {
  width: 80%;
}

code {
  line-height: 1
}
</style>
