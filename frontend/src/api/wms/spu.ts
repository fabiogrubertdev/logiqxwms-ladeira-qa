import http from '@/utils/http/request'

// Função para buscar SKU pelo código de barras
export const getSkuByBarcode = (barcode: string) => http({
  url: '/spu/sku-bar-code/',
  method: 'get',
  params: {
    bar_code: barcode
  }
});