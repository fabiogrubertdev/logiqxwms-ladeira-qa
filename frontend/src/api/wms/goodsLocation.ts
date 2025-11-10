// frontend/src/api/wms/goodsLocation.ts
import http from '@/utils/http/request'
import { PageConfigProps } from '@/types/System/Form'

/**
 * Buscar localização por nome
 * @param locationName Nome da localização (ex: "PCK01LDC01N00")
 * @returns Dados da localização incluindo id
 */
export const getLocationByName = (locationName: string) => http({
  url: '/goodslocation/list',
  method: 'post',
  data: {
    pageIndex: 1,
    pageSize: 1,
    sqlTitle: '',
    searchObjects: [
      {
        sort: 0,
        label: 'location_name',
        name: 'location_name',
        type: 'string',
        operator: 1, // Operador de igualdade
        text: locationName,
        value: locationName,
        comboxItem: []
      }
    ]
  }
})

/**
 * Buscar áreas do depósito
 * @param warehouseId ID do depósito
 * @returns Lista de áreas do depósito
 */
export const getWarehouseAreas = (warehouseId: number) => http({
  url: `/warehousearea/areas-by-warehouse_id/?warehouse_id=${warehouseId}`,
  method: 'get'
})

/**
 * Listar localizações com filtros
 * @param data Configuração de paginação e filtros
 * @returns Lista de localizações
 */
export const listLocations = (data: PageConfigProps) => http({
  url: '/goodslocation/list',
  method: 'post',
  data
})
