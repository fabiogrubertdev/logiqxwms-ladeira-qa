<template>
  <div class="list-container">
    <v-card class="list-card">
      <v-card-title class="list-title">
        {{ $t('router.sideBar.confirmUnloadMobile') }}
      </v-card-title>

      <v-card-text class="list-content">
        <!-- Busca por número do documento (filtra local enquanto digita) -->
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

        <!-- Busca por data (opcional – enter executa busca remota) -->
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

        <v-list lines="two" class="asn-list">
          <template v-if="!data.loading && displayList.length">
            <v-list-item
              v-for="item in displayList"
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

          <div v-else-if="!displayList.length" class="text-center mt-4">
            {{ $t('system.page.noData') }}
          </div>
        </v-list>

        <!-- Carregar mais (paginado) -->
        <div class="text-center mt-4" v-if="!data.loading && method.hasMore() && !data.remoteMode">
          <v-btn variant="outlined" @click="method.loadMore">
            {{ $t('system.page.loadMore') || 'Carregar mais' }}
          </v-btn>
        </div>
      </v-card-text>
    </v-card>
  </div>
</template>

<script lang="ts" setup>
import { reactive, onMounted, computed, watch } from 'vue'
import { useRouter } from 'vue-router'
import { listNew } from '@/api/wms/stockAsn'
import { hookComponent } from '@/components/system'
import i18n from '@/languages/i18n'

const router = useRouter()

const PAGE_SIZE = 50

const data = reactive({
  // filtros do usuário
  searchForm: {
    asn_no: '',
    create_time: '' // esperado YYYY-MM-DD (ajuste se o backend pedir outro)
  },

  // estado de lista e paginação
  baseList: [] as any[],    // lista acumulada “em aberto” (asn_status:0)
  asnList: [] as any[],     // resultados remotos de uma busca (quando remoto)
  total: 0,                 // total retornado pela API (para paginação)
  pageIndex: 1,             // página atual (1-based)
  loading: false,

  // se true, a UI mostra asnList (resultado de busca remota);
  // se false, mostra baseList paginada/filtrada
  remoteMode: false
})

/** Fallback de nome de status caso a API não envie `asn_status_name` */
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

/** Extrai rows no formato do backend enviado por você */
function extractRows(res: any): any[] {
  const rows = res?.data?.rows ?? res?.data?.tableData ?? res?.data ?? []
  return Array.isArray(rows) ? rows : []
}

/** GET página de “em aberto” (asn_status:0), sem filtros adicionais */
async function fetchOpenPage(pageIndex: number) {
  const body = {
    pageIndex,
    pageSize: PAGE_SIZE,
    sqlTitle: 'asn_status:0',
    searchObjects: []
  }
  const { data: res } = await listNew(body)
  const rows = extractRows(res)
  const totals = Number(res?.data?.totals ?? res?.totals ?? 0)
  return { rows, totals }
}

/** Busca remota por campo específico (sem sqlTitle) */
async function fetchBy(field: 'asn_no' | 'create_time', value: string) {
  const body = {
    pageIndex: 1,
    pageSize: PAGE_SIZE,
    searchObjects: [
      {
        name: field,
        operator: field === 'asn_no' ? 1 : 2, // 1=igual; 2=data conforme sua validação
        text: value
      }
    ]
  }
  const { data: res } = await listNew(body)
  const rows = extractRows(res)
  return rows
}

/** Lista exibida na UI (local-filter enquanto digita) */
const displayList = computed(() => {
  const needle = (data.searchForm.asn_no || '').trim().toLowerCase()

  // Modo remoto: exibe resultados da busca, já filtrados para status 0
  if (data.remoteMode) {
    if (!needle) return data.asnList
    return data.asnList.filter((r: any) =>
      String(r?.asn_no ?? '').toLowerCase().includes(needle)
    )
  }

  // Modo local: filtra client-side em cima da base carregada (em aberto)
  if (!needle) return data.baseList
  return data.baseList.filter((r: any) =>
    String(r?.asn_no ?? '').toLowerCase().includes(needle)
  )
})

/** Métodos da tela */
const method = reactive({
  hasMore(): boolean {
    // Só faz sentido no modo local, usando baseList
    if (data.remoteMode) return false
    return data.baseList.length < data.total
  },

  async initOpenList() {
    data.loading = true
    data.remoteMode = false
    data.pageIndex = 1
    try {
      const { rows, totals } = await fetchOpenPage(1)
      // garante apenas status 0 (deve vir assim pela sqlTitle, mas mantemos por segurança)
      data.baseList = dedupById(rows.filter((r: any) => Number(r?.asn_status) === 0))
      data.total = totals
    } catch (e: any) {
      hookComponent.$message({
        type: 'error',
        content: e?.message || i18n.global.t('system.tips.netError')
      })
      data.baseList = []
      data.total = 0
    } finally {
      data.loading = false
    }
  },

  async loadMore() {
    if (!this.hasMore() || data.loading) return
    data.loading = true
    try {
      const next = data.pageIndex + 1
      const { rows } = await fetchOpenPage(next)
      const onlyOpen = rows.filter((r: any) => Number(r?.asn_status) === 0)
      data.baseList = dedupById([...data.baseList, ...onlyOpen])
      data.pageIndex = next
    } catch (e: any) {
      hookComponent.$message({
        type: 'error',
        content: e?.message || i18n.global.t('system.tips.netError')
      })
    } finally {
      data.loading = false
    }
  },

  /** Enter: tenta achar local; se não achar, busca remoto pelo asn_no ou data */
  async sureSearch() {
    const asn = (data.searchForm.asn_no || '').trim()
    const dt = (data.searchForm.create_time || '').trim()

    // 1) Se tem ASN digitado, tenta resolver localmente
    if (asn) {
      const localHit = data.baseList.find((r: any) => String(r?.asn_no) === asn)
      if (localHit) {
        // já está na lista local; só alterna para modo local (garantido) e deixa o filtro agir
        data.remoteMode = false
        return
      }
      // Não achou local → busca remota pelo número
      await this.remoteSearchByAsn(asn)
      return
    }

    // 2) Se não tem ASN mas tem data → busca remota por data
    if (dt) {
      await this.remoteSearchByDate(dt)
      return
    }

    // 3) Sem filtros → volta para modo local e recarrega página 1
    await this.initOpenList()
  },

  async remoteSearchByAsn(asn: string) {
    data.loading = true
    try {
      const rows = await fetchBy('asn_no', asn)
      const onlyOpen = rows.filter((r: any) => Number(r?.asn_status) === 0)
      data.asnList = onlyOpen
      data.remoteMode = true

      if (rows.length > 0 && onlyOpen.length === 0) {
        hookComponent.$message({
          type: 'info',
          content: 'Documento encontrado, porém não está com status em aberto (asn_status ≠ 0).'
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

  async remoteSearchByDate(dt: string) {
    data.loading = true
    try {
      const rows = await fetchBy('create_time', dt)
      const onlyOpen = rows.filter((r: any) => Number(r?.asn_status) === 0)
      data.asnList = onlyOpen
      data.remoteMode = true

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

/** remove duplicados por id ao paginar */
function dedupById(list: any[]) {
  const seen = new Set<number | string>()
  const out: any[] = []
  for (const r of list) {
    const id = r?.id ?? r?.asn_id ?? `${r?.asn_no}-${r?.create_time}`
    if (!seen.has(id)) {
      seen.add(id)
      out.push(r)
    }
  }
  return out
}

/** Redefine para “modo local” quando limpar busca (asn_no + create_time vazios) */
watch(
  () => [data.searchForm.asn_no, data.searchForm.create_time],
  async ([asn, dt], [oldAsn, oldDt]) => {
    if (!asn && !dt && data.remoteMode) {
      // Voltou para busca vazia após uma busca remota → recarrega base local
      await method.initOpenList()
    }
  }
)

onMounted(() => {
  method.initOpenList()
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
