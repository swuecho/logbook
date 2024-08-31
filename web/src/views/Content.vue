<template>
  <el-container>
    <el-header style="text-align: right">
      <div @click="backHome">
        <Icon :icon="icons.homeIcon" height="28" />
      </div>
    </el-header>
    <el-main>
      <div class="content">
        <div v-for="(column, row_idx) in this.summaries" :key="row_idx">
          <el-row>
            <el-col :span="12" v-for="(item, col_index) in column" :key="col_index">
              <el-card class="box-card">
                <div slot="header">
                  <span>
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

<script>
import VueWordCloud from 'vuewordcloud';
import { Icon } from '@iconify/vue2';
import homeIcon from '@iconify/icons-material-symbols/home';
import axios from '@/axiosConfig.js';
export default {
  components: {
    [VueWordCloud.name]: VueWordCloud,
    Icon,
  },
  data() {
    return {
      summaries: [],
      icons: {
        homeIcon,
      },
    };
  },
  async created() {
    await this.fetchDiaryNotes();
  },
  methods: {
    dict_to_lol(dict) {
      var lol = [];
      for (const [key, value] of Object.entries(JSON.parse(dict))) {
        lol.push([key, value])
      }
      return lol;
    },
    backHome() {
      this.$router.push('/')
    },
    async fetchDiaryNotes() {
      try {
        const response = await axios.get('/api/diary');
        const notes = response.data;
        this.processNotes(notes);
      } catch (error) {
        console.error('Error fetching diary notes:', error);
      }
    },
    processNotes(notes) {
      const processedNotes = notes
        .map(note_list => 
          note_list.map(note => ({
            id: note.noteId,
            note: this.dict_to_lol(note.note)
          }))
        );
      console.log('Original notes:', notes);
      console.log('Processed notes:', processedNotes);
      this.summaries = processedNotes;
    }
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
