<template>
  <div class="list-container">
    <v-card class="list-card">
      <v-card-title class="list-title">
        {{ $t('router.sideBar.confirmUnloadMobile') }}
      </v-card-title>
      <v-card-text class="list-content">
        <v-text-field
          v-model="data.searchForm.asn_no"
          :label="$t('wms.stockAsn.asn_no')"
          prepend-inner-icon="mdi-magnify"
          variant="outlined"
          density="compact"
          hide-details
          clearable
          autofocus
          class="mb-4"
          @keyup.enter="method.sureSearch"
        ></v-text-field>
        <v-text-field
          v-model="data.searchForm.create_time"
          :label="$t('wms.stockAsn.create_time')"
          prepend-inner-icon="mdi-calendar"
          variant="outlined"
          density="compact"
          hide-details
          clearable
          class="mb-4"
          @keyup.enter="method.sureSearch"
        ></v-text-field>

        <v-list lines="two" class="asn-list">
          <v-list-item
            v-for="item in data.asnList"
            :key="item.id"
            :title="item.asn_no"
            :subtitle="`${$t('wms.stockAsn.goods_owner_name')}: ${item.goods_owner_name} | ${$t('wms.stockAsn.asn_status')}: ${item.asn_status_name}`"
            @click="method.goToDetail(item.id)"}],path:
            class="asn-list-item"
          >
            <template #append>
              <v-icon color="primary">mdi-chevron-right</v-icon>
            </template>
          </v-list-item>
          <div v-if="data.loading" class="text-center mt-4">
          <v-progress-circular indeterminate color="primary"></v-progress-circular>
        </div>
        <div v-else-if="!data.asnList.length" class="text-center mt-4">
          {{ $t('system.page.noData') }}
        </div>
        </v-list>
      </v-card-text>
    </v-card>
  </div>
</template>

<script lang="ts" setup>
import { reactive, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { listNew } from '@/api/wms/stockAsn'
import { hookComponent } from '@/components/system'
import { i18n } from '@/languages'

const router = useRouter()

const data = reactive({
  searchForm: {
    asn_no: '',
    create_time: ''
  },
  asnList: [] as any[],
  loading: false
})



const method = reactive({
  // Search function
  sureSearch() {
    method.getList()
  },

  // Função para buscar ou listar todos os ASNs pendentes
  async getList() {
    data.loading = true
    
    let requestBody: any = {
      pageIndex: 1,
      pageSize: 100, // Load a reasonable amount for mobile operation
      searchObjects: [],
      total: 0 // PROPRIEDADE ESSENCIAL PARA O BACKEND
    }

    // O filtro de status deve ser sempre 'asn_status:0' (Em Aberto)
    requestBody.sqlTitle = 'asn_status:0'

    // 1. Lógica de Busca por ASN_NO
    if (data.searchForm.asn_no) {
      requestBody.searchObjects.push({
        name: 'asn_no',
        operator: 1, // 1 = Equal (Operador de Igualdade)
        text: data.searchForm.asn_no
      })
    }

    // 2. Lógica de Busca por Data
    if (data.searchForm.create_time) {
      requestBody.searchObjects.push({
        name: 'create_time',
        operator: 2, // 2 = Maior ou Igual (>=)
        text: data.searchForm.create_time
      })
    }



    try {
      // Usar listNew que chama /asn/asnmaster/list
      const { data: res } = await listNew(requestBody)
      if (res.isSuccess) {
        data.asnList = res.data?.tableData || []
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
