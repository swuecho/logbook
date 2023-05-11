<template>
  <div id="stockgrid">
    <v-grid theme="compact" export="true" row-headers="true" :source="rows" :columns="columns"></v-grid>
  </div>
</template>

<script>
import VGrid from "@revolist/vue-datagrid";
import records from "@/records.json"
export default {
  name: "StockList",
  created() {
      let url = 'https://www.bestqa.net/go/api/answers/bestqa_stock_hdjy'
      // let response = fetch(url);

  },
  data() {
    return {
      columns: [
        {
          prop: "id",
          name: "Name",
          sortable: true,
          order: 'asc'
        },
        {
            prop: 'start_time',
            name: "Time"
        },

        {
          prop: "status",
          name: "Price",
          cellTemplate: (createElement, props) => {
            return createElement(
              "span",
              {
                style: {
                  color: "red",
                },
              },
              props.model[props.prop]
            );
          },
        },
        {
            prop: 'gene',
            name: "Volume"
        },
{
            prop: 'Q_BESTQA_PROVINCE',
            name: "Amount"
        }

      ],
      rows: records.filter((x) => x.status == 'D')
    };
  },
  components: {
    VGrid,
  },
};
</script>

<style scoped>
#stockgrid {
  height: 100vh; 
  padding: 0;
  margin: 0;
}
</style>
