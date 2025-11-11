<template>
  <v-dialog v-model="data.showDialog" :width="'90%'" :persistent="true" scrollable>
    <v-card>
      <v-toolbar color="primary" dark>
        <v-toolbar-title>Importar Armazenamento via Excel</v-toolbar-title>
        <v-spacer></v-spacer>
        <v-btn icon @click="method.closeDialog">
          <v-icon>mdi-close</v-icon>
        </v-btn>
      </v-toolbar>
      
      <v-card-text class="pt-4">
        <!-- Seleção de Depósito e Upload -->
        <v-row>
          <v-col cols="4">
            <v-select
              v-model="data.selectedWarehouse"
              :items="data.warehouses"
              item-title="warehouse_name"
              item-value="warehouse_id"
              label="Selecione o Depósito"
              variant="outlined"
              :disabled="data.processing"
            ></v-select>
          </v-col>
          
          <v-col cols="6">
            <v-file-input
              v-model="data.excelFile"
              accept=".xlsx,.xls"
              label="Selecione o arquivo Excel"
              variant="outlined"
              :disabled="data.processing"
              prepend-icon="mdi-file-excel"
              @change="method.handleFileUpload"
            ></v-file-input>
          </v-col>
          
          <v-col cols="2">
            <v-btn
              color="info"
              block
              height="56"
              @click="method.downloadTemplate"
            >
              <v-icon left>mdi-download</v-icon>
              Template
            </v-btn>
          </v-col>
        </v-row>
        
        <!-- Resumo -->
        <v-row v-if="data.previewData.length > 0" class="mb-4">
          <v-col cols="12">
            <v-alert 
              :type="data.errorCount > 0 ? 'warning' : 'success'" 
              variant="tonal"
              border="start"
            >
              <v-row align="center">
                <v-col cols="3">
                  <div class="text-h6">Total: {{ data.previewData.length }}</div>
                </v-col>
                <v-col cols="3">
                  <div class="text-h6 text-success">Válidos: {{ data.validCount }}</div>
                </v-col>
                <v-col cols="3">
                  <div class="text-h6 text-error">Erros: {{ data.errorCount }}</div>
                </v-col>
                <v-col cols="3">
                  <v-progress-linear
                    :model-value="(data.validCount / data.previewData.length) * 100"
                    :color="data.errorCount > 0 ? 'warning' : 'success'"
                    height="25"
                  >
                    <strong>{{ Math.round((data.validCount / data.previewData.length) * 100) }}%</strong>
                  </v-progress-linear>
                </v-col>
              </v-row>
            </v-alert>
          </v-col>
        </v-row>
        
        <!-- Preview dos Dados -->
        <v-card v-if="data.previewData.length > 0" variant="outlined">
          <v-card-title class="bg-grey-lighten-4">
            <v-icon left>mdi-table-eye</v-icon>
            Preview dos Dados
          </v-card-title>
          <vxe-table
            ref="xTable"
            :data="data.previewData"
            :height="400"
            align="center"
            :row-class-name="method.rowClassName"
            border
            stripe
          >
            <template #empty>
              <div class="text-center pa-4">
                <v-icon size="64" color="grey">mdi-table-off</v-icon>
                <p class="text-grey mt-2">Nenhum dado carregado</p>
              </div>
            </template>
            
            <vxe-column type="seq" width="60" title="#"></vxe-column>
            
            <vxe-column field="sku_code" title="SKU" width="150">
              <template #default="{ row }">
                <v-chip 
                  size="small" 
                  :color="row.status === 'OK' ? 'default' : 'error'"
                >
                  {{ row.sku_code }}
                </v-chip>
              </template>
            </vxe-column>
            
            <vxe-column field="location_name" title="Endereço" width="150">
              <template #default="{ row }">
                <v-chip 
                  size="small" 
                  :color="row.location_found ? 'success' : 'error'"
                  prepend-icon="mdi-map-marker"
                >
                  {{ row.location_name }}
                </v-chip>
              </template>
            </vxe-column>
            
            <vxe-column field="putaway_qty" title="Quantidade" width="120" align="right">
              <template #default="{ row }">
                <strong>{{ row.putaway_qty }}</strong>
              </template>
            </vxe-column>
            
            <vxe-column field="series_number" title="Número de Série" width="150"></vxe-column>
            
            <vxe-column field="asn_id" title="ASN ID" width="100" align="center">
              <template #default="{ row }">
                <v-chip 
                  size="small" 
                  :color="row.asn_id ? 'info' : 'default'"
                >
                  {{ row.asn_id || '-' }}
                </v-chip>
              </template>
            </vxe-column>
            
            <vxe-column field="sorted_qty_available" title="Qtd Disponível" width="120" align="right">
              <template #default="{ row }">
                <v-chip 
                  size="small" 
                  :color="row.qty_valid ? 'success' : 'error'"
                >
                  {{ row.sorted_qty_available || '-' }}
                </v-chip>
              </template>
            </vxe-column>
            
            <vxe-column field="status" title="Status" width="100">
              <template #default="{ row }">
                <v-chip 
                  :color="row.status === 'OK' ? 'success' : 'error'"
                  size="small"
                >
                  {{ row.status }}
                </v-chip>
              </template>
            </vxe-column>
            
            <vxe-column field="message" title="Mensagem" min-width="200">
              <template #default="{ row }">
                <div v-if="row.message" class="text-error">
                  <v-icon size="small" color="error">mdi-alert-circle</v-icon>
                  {{ row.message }}
                </div>
                <div v-else class="text-success">
                  <v-icon size="small" color="success">mdi-check-circle</v-icon>
                  Pronto para processar
                </div>
              </template>
            </vxe-column>
          </vxe-table>
        </v-card>
        
        <!-- Instruções -->
        <v-card v-else variant="outlined" class="mt-4">
          <v-card-text>
            <v-row>
              <v-col cols="12" class="text-center">
                <v-icon size="64" color="primary">mdi-file-excel-box</v-icon>
                <h3 class="mt-4">Como usar:</h3>
                <ol class="text-left mt-4" style="max-width: 600px; margin: 0 auto;">
                  <li class="mb-2">Selecione o depósito</li>
                  <li class="mb-2">Faça o download do template Excel</li>
                  <li class="mb-2">Preencha com: SKU, Endereço, Quantidade</li>
                  <li class="mb-2">Faça upload do arquivo preenchido</li>
                  <li class="mb-2">Verifique o preview e clique em "Processar"</li>
                </ol>
              </v-col>
            </v-row>
          </v-card-text>
        </v-card>
      </v-card-text>
      
      <v-divider></v-divider>
      
      <v-card-actions class="pa-4">
        <v-spacer></v-spacer>
        <v-btn 
          variant="text" 
          @click="method.closeDialog"
          :disabled="data.processing"
        >
          Cancelar
        </v-btn>
        <v-btn 
          color="primary" 
          :disabled="data.validCount === 0 || data.processing"
          :loading="data.processing"
          @click="method.processImport"
        >
          <v-icon left>mdi-check</v-icon>
          Processar ({{ data.validCount }} itens)
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<script lang="ts" setup>
import { reactive, ref } from 'vue'
import * as XLSX from 'xlsx'
import { hookComponent } from '@/components/system'
import { confirmPutaway, listNew } from '@/api/wms/stockAsn'
import { getLocationByName } from '@/api/wms/goodsLocation'
import i18n from '@/languages/i18n'

const xTable = ref()
const emit = defineEmits(['success'])

const data = reactive({
  showDialog: false,
  selectedWarehouse: 1, // Default warehouse
  warehouses: [
    { warehouse_id: 1, warehouse_name: '0009' }
  ],
  excelFile: null as any,
  previewData: [] as any[],
  validCount: 0,
  errorCount: 0,
  processing: false,
  asnItemsMap: new Map(), // Mapa: sku_code → asn_item completo
  locationsCache: new Map(), // Cache: location_name → location_data
  currentAsnNo: null as string | null
})

const method = reactive({
  /**
   * Abrir diálogo
   */
  openDialog: async (asnNo: string) => {
    data.currentAsnNo = asnNo
    data.previewData = []
    data.excelFile = null
    data.validCount = 0
    data.errorCount = 0
    
    // Carregar itens disponíveis para armazenamento
    await method.loadAsnItems(asnNo)
    
    data.showDialog = true
  },
  
  /**
   * Fechar diálogo
   */
  closeDialog: () => {
    data.showDialog = false
    data.previewData = []
    data.excelFile = null
    data.asnItemsMap.clear()
    data.locationsCache.clear()
  },
  
  /**
   * Carregar itens disponíveis para armazenamento
   */
   loadAsnItems: async (asnNo: string) => {
    try {
      // Buscar dados do ASN usando /asn/asnmaster/list
      const { data: res } = await listNew({
        pageIndex: 1,
        pageSize: 1,
        searchObjects: [
          {
            name: 'asn_no',
            operator: 1, // Contains
            text: asnNo,
            value: asnNo
          }
        ]
      })
      
      if (res.isSuccess && res.data && res.data.rows && res.data.rows.length > 0) {
        const asnMaster = res.data.rows[0]
        const detailList = asnMaster.detailList || []
        
        // Filtrar apenas itens com status 3 (A Armazenar)
        const itemsToStore = detailList.filter((item: any) => item.asn_status === 3)
        
        // Criar mapa: sku_code → item completo do detailList
        itemsToStore.forEach((item: any) => {
          data.asnItemsMap.set(item.sku_code, item)
        })
        
        console.log(`Carregados ${data.asnItemsMap.size} itens para armazenamento do ASN ${asnNo}`)
      } else {
        hookComponent.$message({
          type: 'warning',
          content: 'ASN não encontrado ou sem itens pendentes de armazenamento'
        })
      }
    } catch (error) {
      console.error('Erro ao carregar itens:', error)
      hookComponent.$message({
        type: 'error',
        content: 'Erro ao carregar itens disponíveis para armazenamento'
      })
    }
  },
  
  /**
   * Processar upload do Excel
   */
  handleFileUpload: async (event: any) => {
    const files = event.target.files || event
    if (!files || files.length === 0) return
    
    const file = files[0]
    
    // Ler Excel
    const reader = new FileReader()
    reader.onload = async (e: any) => {
      try {
        const workbook = XLSX.read(e.target.result, { type: 'binary' })
        const sheetName = workbook.SheetNames[0]
        const worksheet = workbook.Sheets[sheetName]
        const excelData = XLSX.utils.sheet_to_json(worksheet)
        
        if (excelData.length === 0) {
          hookComponent.$message({
            type: 'warning',
            content: 'O arquivo Excel está vazio'
          })
          return
        }
        
        // Processar e validar dados
        await method.validateData(excelData)
      } catch (error) {
        console.error('Erro ao ler Excel:', error)
        hookComponent.$message({
          type: 'error',
          content: 'Erro ao ler arquivo Excel. Verifique o formato.'
        })
      }
    }
    reader.readAsBinaryString(file)
  },
  
  /**
   * Validar dados do Excel
   */
  validateData: async (excelData: any[]) => {
    data.previewData = []
    data.validCount = 0
    data.errorCount = 0
    
    hookComponent.$message({
      type: 'info',
      content: `Validando ${excelData.length} linhas...`
    })
    
    for (let i = 0; i < excelData.length; i++) {
      const row = excelData[i]
      
      const validatedRow: any = {
        sku_code: row['SKU'] || row['sku_code'] || row['Código'] || '',
        location_name: row['Endereço'] || row['Endereco'] || row['location_name'] || row['Localização'] || row['Localizacao'] || '',
        putaway_qty: parseInt(row['Quantidade'] || row['quantidade'] || row['putaway_qty'] || row['Qtd'] || '0'),
        series_number: row['Número de Série'] || row['Numero de Serie'] || row['series_number'] || row['SN'] || '',
        asn_id: null, // ID do item do detailList
        status: 'OK',
        message: '',
        location_found: false,
        qty_valid: false,
        sorted_qty_available: null
      }
      
      // Validação 1: Campos obrigatórios
      if (!validatedRow.sku_code) {
        validatedRow.status = 'ERRO'
        validatedRow.message = 'SKU não informado'
        data.errorCount++
      } else if (!validatedRow.location_name) {
        validatedRow.status = 'ERRO'
        validatedRow.message = 'Endereço não informado'
        data.errorCount++
      } else if (!validatedRow.putaway_qty || validatedRow.putaway_qty <= 0) {
        validatedRow.status = 'ERRO'
        validatedRow.message = 'Quantidade inválida'
        data.errorCount++
      }
      // Validação 2: SKU existe em "A Armazenar"?
      else if (!data.asnItemsMap.has(validatedRow.sku_code)) {
        validatedRow.status = 'ERRO'
        validatedRow.message = 'SKU não encontrado em "A Armazenar"'
        data.errorCount++
      }
      // Validação 3: Quantidade não excede disponível
      else {
        const asnItem = data.asnItemsMap.get(validatedRow.sku_code)
        validatedRow.asn_id = asnItem.id // ID do item do detailList
        validatedRow.sorted_qty_available = asnItem.sorted_qty
        
        if (validatedRow.putaway_qty > asnItem.sorted_qty) {
          validatedRow.status = 'ERRO'
          validatedRow.message = `Quantidade excede disponível (${asnItem.sorted_qty})`
          validatedRow.qty_valid = false
          data.errorCount++
        } else {
          validatedRow.qty_valid = true
          
          // Validação 4: Endereço existe?
          try {
            const location = await method.findLocation(validatedRow.location_name)
            if (!location) {
              validatedRow.status = 'ERRO'
              validatedRow.message = 'Endereço não encontrado no sistema'
              validatedRow.location_found = false
              data.errorCount++
            } else {
              validatedRow.location_found = true
              validatedRow.location_id = location.id
              data.validCount++
            }
          } catch (error) {
            validatedRow.status = 'ERRO'
            validatedRow.message = 'Erro ao buscar endereço'
            validatedRow.location_found = false
            data.errorCount++
          }
        }
      }
      
      data.previewData.push(validatedRow)
    }
    
    hookComponent.$message({
      type: data.errorCount > 0 ? 'warning' : 'success',
      content: `Validação concluída: ${data.validCount} válidos, ${data.errorCount} com erro`
    })
  },
  
  /**
   * Buscar localização por nome (com cache)
   */
  findLocation: async (locationName: string) => {
    // Verificar cache
    if (data.locationsCache.has(locationName)) {
      return data.locationsCache.get(locationName)
    }
    
    // Buscar no backend
    try {
      const { data: res } = await getLocationByName(locationName)
      if (res.isSuccess && res.data && res.data.rows && res.data.rows.length > 0) {
        const location = res.data.rows[0]
        data.locationsCache.set(locationName, location)
        return location
      }
    } catch (error) {
      console.error('Erro ao buscar localização:', error)
    }
    
    return null
  },
  
  /**
   * Processar importação
   */
  processImport: async () => {
    if (data.validCount === 0) {
      hookComponent.$message({
        type: 'warning',
        content: 'Não há itens válidos para processar'
      })
      return
    }
    
    data.processing = true
    
    try {
      // Preparar payload para API
      const putawayList: any[] = []
      
      for (const row of data.previewData) {
        if (row.status === 'OK') {
          const asnItem = data.asnItemsMap.get(row.sku_code)
          
          putawayList.push({
            asn_id: row.asn_id, // ID do item do detailList (já validado)
            goods_owner_id: asnItem.goods_owner_id || 0,
            series_number: row.series_number || '',
            goods_location_id: row.location_id,
            putaway_qty: row.putaway_qty
          })
        }
      }
      
      console.log('Enviando para API:', putawayList)
      
      // Chamar API de armazenamento
      const logTemp = {} // Pode adicionar informações de log se necessário
      const { data: res } = await confirmPutaway(putawayList, logTemp)
      
      if (res.isSuccess) {
        hookComponent.$message({
          type: 'success',
          content: `${putawayList.length} itens armazenados com sucesso!`
        })
        
        emit('success')
        method.closeDialog()
      } else {
        hookComponent.$message({
          type: 'error',
          content: res.errorMessage || 'Erro ao processar armazenamento'
        })
      }
    } catch (error: any) {
      console.error('Erro ao processar importação:', error)
      hookComponent.$message({
        type: 'error',
        content: error.message || 'Erro ao processar importação'
      })
    } finally {
      data.processing = false
    }
  },
  
  /**
   * Download do template Excel
   */
  downloadTemplate: () => {
    // Criar dados de exemplo
    const templateData = [
      {
        'SKU': 'SKU001',
        'Endereço': 'PCK01LDC01N00',
        'Quantidade': 10,
        'Número de Série': ''
      },
      {
        'SKU': 'SKU002',
        'Endereço': 'PCK01LDC01N01',
        'Quantidade': 15,
        'Número de Série': 'SN123'
      }
    ]
    
    // Criar workbook
    const ws = XLSX.utils.json_to_sheet(templateData)
    const wb = XLSX.utils.book_new()
    XLSX.utils.book_append_sheet(wb, ws, 'Armazenamento')
    
    // Download
    XLSX.writeFile(wb, 'Template_Armazenamento.xlsx')
    
    hookComponent.$message({
      type: 'success',
      content: 'Template baixado com sucesso!'
    })
  },
  
  /**
   * Classe CSS para linhas com erro
   */
  rowClassName: ({ row }: any) => {
    if (row.status === 'ERRO') {
      return 'row-error'
    }
    return ''
  }
})

defineExpose({
  openDialog: method.openDialog
})
</script>

<style scoped lang="less">
:deep(.row-error) {
  background-color: #ffebee !important;
}

:deep(.vxe-body--row:hover.row-error) {
  background-color: #ffcdd2 !important;
}
</style>
