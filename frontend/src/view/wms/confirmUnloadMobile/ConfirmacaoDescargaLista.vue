<template>
  <div class="list-container">
    <v-card class="list-card">
      <v-card-title class="list-title">
        {{ $t('router.sideBar.confirmUnloadMobile') }}
      </v-card-title>
      <v-card-text class="list-content">
        <v-text-field
          v-model="data.search"
          :label="$t('system.page.search')"
          prepend-inner-icon="mdi-magnify"
          variant="outlined"
          density="compact"
          hide-details
          clearable
          class="mb-4"
          @keyup.enter="method.sureSearch"
        ></v-text-field>

        <v-list lines="two" class="asn-list">
          <v-list-item
            v-for="item in filteredList"
            :key="item.asn_id"
            :title="item.asn_no"
            :subtitle="`${$t('wms.stockAsn.owner_name')}: ${item.owner_name} | ${$t('wms.stockAsn.asn_status')}: ${item.asn_status_name}`"
            @click="method.goToDetail(item.asn_id)"
            class="asn-list-item"
          >
            <template #append>
              <v-icon color="primary">mdi-chevron-right</v-icon>
            </template>
          </v-list-item>
          <v-list-item v-if="filteredList.length === 0">
            <v-list-item-title class="text-center">
              {{ $t('system.tips.noData') }}
            </v-list-item-title>
          </v-list-item>
        </v-list>
      </v-card-text>
    </v-card>
  </div>
</template>

<script lang="ts" setup>
import { reactive, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { getStockAsnList } from '@/api/wms/stockAsn'
import { setSearchObject } from '@/utils/common'
import { SearchObject } from '@/types/System/Form'
import { hookComponent } from '@/components/system'
import i18n from '@/languages/i18n'

const router = useRouter()

const data = reactive({
  search: '',
  asnList: [] as any[],
  loading: false,
  searchForm: {
    asn_no: ''
  }
})

const filteredList = computed(() => {
  // A filtragem local não é mais necessária, pois a busca será feita na API
  return data.asnList
})

const method = reactive({
  // Search function
  sureSearch() {
    data.searchForm.asn_no = data.search
    method.getList()
  },

  // Get ASN list with status 'A Separar' (2)
  async getList() {
    data.loading = true
    let sqlTitle = 'asn_status:2' // Status 'A Separar'
    let searchObjects: SearchObject[] = []

    if (data.searchForm.asn_no) {
      // Se houver busca, usa setSearchObject para construir o filtro
      // O setSearchObject deve ser capaz de converter { asn_no: 'valor' } para o formato esperado pelo backend
      searchObjects = setSearchObject(data.searchForm)
      sqlTitle = '' // Limpa o sqlTitle se houver searchObjects
    }

    try {
      const { data: res } = await getStockAsnList({
        pageIndex: 1,
        pageSize: 100, // Load a reasonable amount for mobile operation
        sqlTitle: sqlTitle,
        searchObjects: searchObjects
      })
      if (res.isSuccess) {
        data.asnList = res.data.tableData
      } else {
        hookComponent.$message({
          type: 'error',
          content: res.errorMessage || i18n.global.t('system.tips.netError')
        })
      }
    } catch (e) {
      hookComponent.$message({
        type: 'error',
        content: i18n.global.t('system.tips.netError')
      })
    } finally {
      data.loading = false
    }
  },

  // Navigate to detail screen
  goToDetail(asnId: number) {
    router.push({
      path: '/confirmUnloadMobileDetail',
      query: { asnId }
    })
  }
})

onMounted(() => {
  method.getList()
})
</script>

<style scoped lang="less">
.list-container {
  padding: 10px;
  height: 100%;
  display: flex;
  flex-direction: column;
}

.list-card {
  flex-grow: 1;
  display: flex;
  flex-direction: column;
}

.list-title {
  font-size: 1.25rem;
  font-weight: 500;
  padding: 16px;
  border-bottom: 1px solid #eee;
}

.list-content {
  flex-grow: 1;
  overflow-y: auto;
  padding: 16px;
}

.asn-list {
  padding: 0;
}

.asn-list-item {
  border-bottom: 1px solid #f0f0f0;
  cursor: pointer;
  transition: background-color 0.2s;
}

.asn-list-item:hover {
  background-color: #f5f5f5;
}
</style>
