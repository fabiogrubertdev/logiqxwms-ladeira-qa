<template>
  <div class="detail-container">
    <v-card class="detail-card">
      <v-card-title class="detail-title">
        {{ $t('router.sideBar.confirmUnloadMobile') }} - ASN: {{ data.asnMaster.asn_no }}
      </v-card-title>
      <v-card-text class="detail-content">
        <v-text-field
          ref="barcodeInput"
          v-model="data.barcode"
          :label="$t('wms.stockAsn.barcode')"
          prepend-inner-icon="mdi-barcode-scan"
          variant="outlined"
          density="compact"
          hide-details
          clearable
          autofocus
          @keyup.enter="method.handleBarcodeScan"
          class="mb-4"
        ></v-text-field>

        <v-divider class="my-4"></v-divider>

        <div class="scan-result">
          <p class="text-h6 mb-2">SKU Bipado:</p>
          <v-chip color="primary" size="large" class="mb-4">
            {{ data.scannedSkuCode || 'Aguardando Bipagem...' }}
          </v-chip>

          <p class="text-h6 mb-2">Local de Destino:</p>
          <v-chip :color="data.targetLocation === '---' ? 'error' : 'success'" size="x-large" class="target-location-chip">
            {{ data.targetLocation || '---' }}
          </v-chip>
        </div>

        <v-divider class="my-4"></v-divider>

        <v-list lines="two" class="asn-detail-list">
          <v-list-subheader>Itens do ASN ({{ data.asnMaster.detailList.length }})</v-list-subheader>
          <v-list-item
            v-for="item in data.asnMaster.detailList"
            :key="item.id"
            :title="item.sku_code"
            :subtitle="`${$t('wms.stockAsn.sku_name')}: ${item.sku_name} | ${$t('wms.stockAsn.qty')}: ${item.qty}`"
            :class="{ 'scanned-item': item.sku_code === data.scannedSkuCode }"
          >
            <template #append>
              <v-chip size="small" :color="item.sku_code === data.scannedSkuCode ? 'success' : 'grey'">
                {{ item.goods_location_name }}
              </v-chip>
            </template>
          </v-list-item>
        </v-list>
      </v-card-text>
    </v-card>
  </div>
</template>

<script lang="ts" setup>
import { reactive, onMounted, ref, nextTick } from 'vue'
import { useRoute } from 'vue-router'
import { getStockAsnList } from '@/api/wms/stockAsn'
import { hookComponent } from '@/components/system'
import i18n from '@/languages/i18n'
import { getSkuByBarcode } from '@/api/wms/spu' // Nova API

const route = useRoute()
const barcodeInput = ref<HTMLInputElement | null>(null)

const data = reactive({
  asnMaster: {
    asn_no: 'Carregando...',
    detailList: [] as any[]
  } as any,
  barcode: '',
  scannedSkuCode: '',
  targetLocation: ''
})

const method = reactive({
  // Load ASN details
  async loadAsnDetails() {
    const asnId = route.query.asnId
    if (!asnId) {
      hookComponent.$message({
        type: 'error',
        content: 'ASN ID não fornecido.'
      })
      return
    }

    try {
      // Reusing getStockAsnList to get details, assuming it returns detailList
      const { data: res } = await getStockAsnList({
        pageIndex: 1,
        pageSize: 1,
        searchObjects: [{ searchProperty: 'id', searchValue: asnId, searchOperation: 0 }]
      })

      if (res.isSuccess && res.data.tableData.length > 0) {
        data.asnMaster = res.data.tableData[0]
        // Ensure detailList is loaded
        if (!data.asnMaster.detailList || data.asnMaster.detailList.length === 0) {
          hookComponent.$message({
            type: 'warning',
            content: 'ASN sem itens detalhados para conferência.'
          })
        }
        nextTick(() => {
          if (barcodeInput.value) {
            barcodeInput.value.focus()
          }
        })
      } else {
        hookComponent.$message({
          type: 'error',
          content: 'ASN não encontrado ou erro ao carregar detalhes.'
        })
      }
    } catch (e) {
      hookComponent.$message({
        type: 'error',
        content: i18n.global.t('system.tips.netError')
      })
    }
  },

  // Handle barcode scan
  async handleBarcodeScan() {
    const barcodeValue = data.barcode
    data.barcode = '' // Clear input immediately

    if (!barcodeValue) return

    // 1. Get SKU code from barcode
    const { data: res }: any = await getSkuByBarcode(barcodeValue)

    if (res.isSuccess) {
      const scannedSkuCode = res.data.sku_code
      data.scannedSkuCode = scannedSkuCode

      // 2. Find item in ASN detail list
      const itemFound = data.asnMaster.detailList.find(
        (item: any) => item.sku_code === scannedSkuCode
      )

      if (itemFound) {
        data.targetLocation = itemFound.goods_location_name || 'LOCAL NÃO DEFINIDO'
        hookComponent.$message({
          type: 'success',
          content: `Item ${scannedSkuCode} encontrado. Destino: ${data.targetLocation}`
        })
      } else {
        data.targetLocation = '---'
        hookComponent.$message({
          type: 'warning',
          content: `SKU ${scannedSkuCode} não pertence a este ASN.`
        })
      }
    } else {
      data.scannedSkuCode = '---'
      data.targetLocation = '---'
      hookComponent.$message({
        type: 'error',
        content: res.errorMessage || 'Erro ao buscar SKU.'
      })
    }

    // Re-focus input for next scan
    nextTick(() => {
      if (barcodeInput.value) {
        barcodeInput.value.focus()
      }
    })
  }
})

onMounted(() => {
  method.loadAsnDetails()
})
</script>

<style scoped lang="less">
.detail-container {
  padding: 10px;
  height: 100%;
  display: flex;
  flex-direction: column;
}

.detail-card {
  flex-grow: 1;
  display: flex;
  flex-direction: column;
}

.detail-title {
  font-size: 1.25rem;
  font-weight: 500;
  padding: 16px;
  border-bottom: 1px solid #eee;
}

.detail-content {
  flex-grow: 1;
  overflow-y: auto;
  padding: 16px;
}

.scan-result {
  text-align: center;
  padding: 10px 0;
}

.target-location-chip {
  font-size: 1.5rem;
  height: auto;
  padding: 10px 20px;
}

.asn-detail-list {
  padding: 0;
}

.scanned-item {
  background-color: #e8f5e9; /* Light green for scanned item */
  border-left: 5px solid #4caf50;
}
</style>
