<template>
  <div class="list-container">
    <v-card class="list-card">
      <v-card-title class="list-title">
        {{ $t('router.sideBar.confirmUnloadMobile') }}
      </v-card-title>

      <v-card-text class="list-content">
        <!-- Busca por número do documento -->
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
        />

        <!-- Busca por data (opcional) -->
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
        />

        <!-- Lista -->
        <v-list lines="two" class="asn-list">
          <template v-if="!data.loading && data.asnList.length">
            <v-list-item
              v-for="item in data.asnList"
              :key="item.id"
              :title="item.asn_no"
              :subtitle="`${$t('wms.stockAsn.goods_owner_name')}: ${item.goods_owner_name ?? ''} | ${$t('wms.stockAsn.asn_status')}: ${item.asn_status_name ?? statusName(item.asn_status)}`"
              @click="method.goToDetail(item.id)"
              class="asn-list-item"
            >
              <template #append>
                <v-icon color="primary">mdi-chevron-right</v-icon>
              </template>
            </v-list-item>
          </template>

          <div v-if="data.loading" class="text-center mt-4">
            <v-progress-circular indeterminate color="primary" />
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
    create_time: '' // esperado no formato YYYY-MM-DD (ajuste se teu backend pedir outro)
  },
  asnList: [] as any[],
  loading: false
})

/** Nome de status fallback caso a API não envie asn_status_name */
function statusName(s: number | string | undefined) {
  const v = Number(s)
  const map: Record<number, string> = {
    0: 'Em aberto',
    1: 'Em processamento',
    2: 'A separar',
    3: 'Separado',
    4: 'Finalizado'
  }
  return map[v] ?? String(s ?? '')
}

function extractRows(res: any): any[] {
  const rows = res?.data?.rows ?? res?.data?.tableData ?? res?.data ?? []
  return Array.isArray(rows) ? rows : []
}

async function fetchOpen() {
  // Lista “em aberto” (asn_status:0) — sem searchObjects
  const body = {
    pageIndex: 1,
    pageSize: 50,
    sqlTitle: 'asn_status:0',
    searchObjects: []
  }
  const { data: res } = await listNew(body)
  return extractRows(res)
}

async function fetchBy(field: 'asn_no' | 'create_time', value: string) {
  // Busca específica — sem sqlTitle
  const body = {
    pageIndex: 1,
    pageSize: 50,
    searchObjects: [
      {
        name: field,
        operator: field === 'asn_no' ? 1 : 2, // 1=igual; 2=data (>=) conforme teu exemplo
        text: value
      }
    ]
  }
  const { data: res } = await listNew(body)
  return extractRows(res)
}

const method = reactive({
  sureSearch() {
    this.getList()
  },

  async getList() {
    data.loading = true
    try {
      const asn = (data.searchForm.asn_no || '').trim()
      const dt = (data.searchForm.create_time || '').trim()

      if (!asn && !dt) {
        // Sem filtros -> lista em aberto
        const rows = await fetchOpen()
        data.asnList = rows
        if (!rows.length) {
          hookComponent.$message({
            type: 'warning',
            content: i18n.global.t('system.tips.noData')
          })
        }
        return
      }

      // Com busca: prioridade para asn_no; se não houver, usa data
      const field = asn ? 'asn_no' : 'create_time'
      const value = asn || dt

      let rows = await fetchBy(field, value)

      // Regra de exibição: mostrar somente em aberto (asn_status === 0)
      const onlyOpen = rows.filter((r: any) => Number(r?.asn_status) === 0)
      data.asnList = onlyOpen

      if (rows.length > 0 && onlyOpen.length === 0) {
        hookComponent.$message({
          type: 'info',
          content: 'Documento(s) encontrado(s), porém não estão com status em aberto (asn_status ≠ 0).'
        })
      } else if (rows.length === 0) {
        hookComponent.$message({
          type: 'warning',
          content: i18n.global.t('system.tips.noData')
        })
      }
    } catch (e: any) {
      hookComponent.$message({
        type: 'error',
        content: e?.message || i18n.global.t('system.tips.netError')
      })
    } finally {
      data.loading = false
    }
  },

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
.text-center {
  text-align: center;
}
.mt-4 {
  margin-top: 1rem;
}
</style>
