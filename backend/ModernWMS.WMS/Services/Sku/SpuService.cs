/*
 * date：2022-12-21
 * developer：AMo
 */
using Mapster;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ModernWMS.Core.DBContext;
using ModernWMS.Core.DynamicSearch;
using ModernWMS.Core.JWT;
using ModernWMS.Core.Models;
using ModernWMS.Core.Services;
using ModernWMS.WMS.Entities.Models;
using ModernWMS.WMS.Entities.ViewModels;
using ModernWMS.WMS.IServices;

namespace ModernWMS.WMS.Services
{
    /// <summary>
    ///  Spu Service
    /// </summary>
    public class SpuService : BaseService<SpuEntity>, ISpuService
    {
        #region Args
        private readonly SqlDBContext _dBContext;
        private readonly IStringLocalizer<Core.MultiLanguage> _stringLocalizer;
        private readonly IWebHostEnvironment _webHostEnvironment;
        #endregion

        #region constants & helpers (barcodes + images)
        private readonly string[] _imageRelativePath = new[] { "sku_images" };

        /// Split barcodes tolerando ',', ';', espaços e quebras; trim + distinct.
        private static List<string> SplitBarCodes(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return new List<string>();
            var tokens = s.Split(new[] { ',', ';', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                          .Select(x => x.Trim())
                          .Where(x => !string.IsNullOrWhiteSpace(x))
                          .Distinct(StringComparer.OrdinalIgnoreCase)
                          .ToList();
            return tokens;
        }

        /// Normaliza como string única separada por vírgulas; limita 128 chars.
        private static string NormalizeBarCode(string? s)
        {
            var list = SplitBarCodes(s);
            if (list.Count == 0) return string.Empty;

            var joined = string.Join(",", list);
            if (joined.Length <= 128) return joined;

            var acc = new List<string>();
            int length = 0;
            foreach (var code in list)
            {
                var addLen = (acc.Count == 0 ? 0 : 1) + code.Length; // +1 pela vírgula
                if (length + addLen > 128) break;
                acc.Add(code);
                length += addLen;
            }
            return string.Join(",", acc);
        }

        private string GetImageStorageDirectory()
        {
            var directoryParts = new List<string> { _webHostEnvironment.WebRootPath };
            directoryParts.AddRange(_imageRelativePath);
            string fullPath = Path.Combine(directoryParts.ToArray());
            if (!Directory.Exists(fullPath)) Directory.CreateDirectory(fullPath);
            return fullPath;
        }

        private string GetFileNameFromUrl(string imageUrl) =>
            string.IsNullOrEmpty(imageUrl) ? string.Empty : Path.GetFileName(imageUrl);

        private bool IsValidImageFileName(string fileName) =>
            !string.IsNullOrEmpty(fileName) && fileName.StartsWith("sku-img_") && Path.HasExtension(fileName);
        #endregion

        #region constructor
        public SpuService(
            SqlDBContext dBContext,
            IStringLocalizer<Core.MultiLanguage> stringLocalizer,
            IWebHostEnvironment webHostEnvironment
        )
        {
            _dBContext = dBContext;
            _stringLocalizer = stringLocalizer;
            _webHostEnvironment = webHostEnvironment;
        }
        #endregion

        #region Api
        /// <summary>
        /// page search (com suporte a filtro por detailList.sku_code)
        /// </summary>
        public async Task<(List<SpuBothViewModel> data, int totals)> PageAsync(PageSearch pageSearch, CurrentUser currentUser)
        {
            // DbSets
            var Categorys       = _dBContext.GetDbSet<CategoryEntity>();
            var Spus            = _dBContext.GetDbSet<SpuEntity>();
            var Skus            = _dBContext.GetDbSet<SkuEntity>();
            var SkuSafetyStocks = _dBContext.GetDbSet<SkuSafetyStockEntity>();
            var Warehouses      = _dBContext.GetDbSet<WarehouseEntity>();

            // 1) Separa filtros "seguros" (DynamicSearch) do filtro especial por SKU
            var rawFilters = pageSearch?.searchObjects ?? new List<ModernWMS.Core.DynamicSearch.SearchObject>();

            // Importante: SearchObject usa propriedades PascalCase (Name, Operator, Text, Value, ...)
            var skuFilters = rawFilters
                .Where(s => string.Equals(s.Name, "detailList.sku_code", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var safeFilters = rawFilters
                .Where(s => !string.Equals(s.Name, "detailList.sku_code", StringComparison.OrdinalIgnoreCase))
                .ToList();

            QueryCollection queries = new();
            if (safeFilters.Any())
            {
                safeFilters.ForEach(s => queries.Add(s));
            }

            // 2) Base: por tenant
            IQueryable<SpuEntity> baseSpuQuery = Spus.AsNoTracking()
                .Where(m => m.tenant_id == currentUser.tenant_id);

            // 3) Aplica filtro especial por SKU (antes da projeção), evitando predicate nulo
            if (skuFilters.Any())
            {
                var f     = skuFilters[0];
                var token = (f.Text ?? f.Value?.ToString() ?? string.Empty).Trim();
                var op    = f.Operator; // 0 = Equals, 1 = Contains

                if (!string.IsNullOrEmpty(token))
                {
                    if (op == 0) // Equals
                    {
                        baseSpuQuery = baseSpuQuery.Where(m =>
                            Skus.Any(k => k.spu_id == m.id && k.sku_code == token));
                    }
                    else // Contains
                    {
                        baseSpuQuery = baseSpuQuery.Where(m =>
                            Skus.Any(k => k.spu_id == m.id && k.sku_code.Contains(token)));
                    }
                }
            }

            // 4) Projeção
            var projected = from m in baseSpuQuery
                            join c in Categorys.AsNoTracking() on m.category_id equals c.id
                            select new SpuBothViewModel
                            {
                                id = m.id,
                                spu_code = m.spu_code,
                                spu_name = m.spu_name,
                                category_id = m.category_id,
                                category_name = c.category_name,
                                spu_description = m.spu_code,
                                supplier_id = m.supplier_id,
                                supplier_name = m.supplier_name,
                                brand = m.brand,
                                origin = m.origin,
                                length_unit = m.length_unit,
                                volume_unit = m.volume_unit,
                                weight_unit = m.weight_unit,
                                creator = m.creator,
                                create_time = m.create_time,
                                last_update_time = m.last_update_time,
                                is_valid = m.is_valid,
                                detailList = Skus.AsNoTracking().Where(t => t.spu_id.Equals(m.id))
                                    .Select(t => new SkuViewModel
                                    {
                                        id = t.id,
                                        spu_id = t.spu_id,
                                        sku_code = t.sku_code,
                                        sku_name = t.sku_name,
                                        image_url = t.image_url,
                                        bar_code = t.bar_code,
                                        weight = t.weight,
                                        lenght = t.lenght,
                                        width = t.width,
                                        height = t.height,
                                        volume = t.volume,
                                        unit = t.unit,
                                        cost = t.cost,
                                        price = t.price,
                                        create_time = t.create_time,
                                        last_update_time = t.last_update_time,
                                        detailList = (from sss in SkuSafetyStocks.AsNoTracking()
                                                      join wh in Warehouses on sss.warehouse_id equals wh.id
                                                      where sss.sku_id.Equals(t.id)
                                                      select new SkuSafetyStockViewModel
                                                      {
                                                          id = sss.id,
                                                          sku_id = sss.sku_id,
                                                          safety_stock_qty = sss.safety_stock_qty,
                                                          warehouse_id = sss.warehouse_id,
                                                          warehouse_name = wh.warehouse_name
                                                      }).ToList()
                                    }).ToList()
                            };

            // 5) Filtros "seguros" (DynamicSearch) — aplica só se houver predicate
            var predicate = queries.AsExpression<SpuBothViewModel>();
            if (predicate != null)
            {
                projected = projected.Where(predicate);
            }

            // 6) Totais e paginação (inalterado)
            int totals;
            List<SpuBothViewModel> list;
            if (pageSearch.pageIndex <= 0 || pageSearch.pageSize <= 0)
            {
                totals = await projected.CountAsync();
                list = await projected.OrderByDescending(t => t.create_time).ToListAsync();
            }
            else
            {
                totals = await projected.CountAsync();
                list = await projected.OrderByDescending(t => t.create_time)
                                      .Skip((pageSearch.pageIndex - 1) * pageSearch.pageSize)
                                      .Take(pageSearch.pageSize)
                                      .ToListAsync();
            }

            return (list, totals);
        }

        public async Task<SpuBothViewModel> GetAsync(int id)
        {
            var Categorys = _dBContext.GetDbSet<CategoryEntity>();
            var Spus = _dBContext.GetDbSet<SpuEntity>();
            var Skus = _dBContext.GetDbSet<SkuEntity>();
            var SkuSafetyStocks = _dBContext.GetDbSet<SkuSafetyStockEntity>();
            var Warehouses = _dBContext.GetDbSet<WarehouseEntity>();

            var query = from m in Spus.AsNoTracking()
                        join c in Categorys.AsNoTracking() on m.category_id equals c.id
                        where m.id == id
                        select new SpuBothViewModel
                        {
                            id = m.id,
                            spu_code = m.spu_code,
                            spu_name = m.spu_name,
                            category_id = m.category_id,
                            category_name = c.category_name,
                            spu_description = m.spu_description,
                            supplier_id = m.supplier_id,
                            supplier_name = m.supplier_name,
                            brand = m.brand,
                            origin = m.origin,
                            length_unit = m.length_unit,
                            volume_unit = m.volume_unit,
                            weight_unit = m.weight_unit,
                            creator = m.creator,
                            create_time = m.create_time,
                            last_update_time = m.last_update_time,
                            is_valid = m.is_valid,
                            detailList = Skus.Where(t => t.spu_id.Equals(m.id))
                                         .Select(t => new SkuViewModel
                                         {
                                             id = t.id,
                                             spu_id = t.spu_id,
                                             sku_code = t.sku_code,
                                             sku_name = t.sku_name,
                                             bar_code = t.bar_code,
                                             image_url = t.image_url,
                                             weight = t.weight,
                                             lenght = t.lenght,
                                             width = t.width,
                                             height = t.height,
                                             volume = t.volume,
                                             unit = t.unit,
                                             cost = t.cost,
                                             price = t.price,
                                             create_time = t.create_time,
                                             last_update_time = t.last_update_time,
                                             detailList = (from sss in SkuSafetyStocks.AsNoTracking()
                                                           join wh in Warehouses on sss.warehouse_id equals wh.id
                                                           where sss.sku_id.Equals(t.id)
                                                           select new SkuSafetyStockViewModel
                                                           {
                                                               id = sss.id,
                                                               sku_id = sss.sku_id,
                                                               safety_stock_qty = sss.safety_stock_qty,
                                                               warehouse_id = sss.warehouse_id,
                                                               warehouse_name = wh.warehouse_name
                                                           }).ToList()
                                         }).ToList()
                        };

            var data = await query.FirstOrDefaultAsync();
            return data ?? new SpuBothViewModel();
        }

        public async Task<SkuDetailViewModel> GetSkuAsync(int sku_id)
        {
            var Categorys = _dBContext.GetDbSet<CategoryEntity>();
            var Spus = _dBContext.GetDbSet<SpuEntity>();
            var Skus = _dBContext.GetDbSet<SkuEntity>();

            var query = from m in Spus.AsNoTracking()
                        join c in Categorys.AsNoTracking() on m.category_id equals c.id
                        join d in Skus.AsNoTracking() on m.id equals d.spu_id
                        where d.id == sku_id
                        select new SkuDetailViewModel
                        {
                            spu_id = m.id,
                            spu_code = m.spu_code,
                            spu_name = m.spu_name,
                            category_id = m.category_id,
                            category_name = c.category_name,
                            spu_description = m.spu_description,
                            supplier_id = m.supplier_id,
                            supplier_name = m.supplier_name,
                            brand = m.brand,
                            origin = m.origin,
                            length_unit = m.length_unit,
                            volume_unit = m.volume_unit,
                            weight_unit = m.weight_unit,
                            sku_id = d.id,
                            sku_code = d.sku_code,
                            sku_name = d.sku_name,
                            bar_code = d.bar_code,
                            image_url = d.image_url,
                            weight = d.weight,
                            lenght = d.lenght,
                            width = d.width,
                            height = d.height,
                            volume = d.volume,
                            unit = d.unit,
                            cost = d.cost,
                            price = d.price
                        };

            var data = await query.FirstOrDefaultAsync();
            return data ?? new SkuDetailViewModel();
        }

        /// <summary>
        /// get sku info by bar_code (suporta múltiplos códigos no mesmo campo)
        /// </summary>
        public async Task<SkuDetailViewModel> GetSkuByBarCodeAsync(string bar_code)
        {
            var token = (bar_code ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(token)) return new SkuDetailViewModel();

            var Categorys = _dBContext.GetDbSet<CategoryEntity>();
            var Spus = _dBContext.GetDbSet<SpuEntity>();
            var Skus = _dBContext.GetDbSet<SkuEntity>();

            // 1) reduz candidatos no banco via LIKE
            var candidates = await (
                from m in Spus.AsNoTracking()
                join c in Categorys.AsNoTracking() on m.category_id equals c.id
                join d in Skus.AsNoTracking() on m.id equals d.spu_id
                where d.bar_code != null && d.bar_code.Contains(token)
                select new { m, c, d }
            ).ToListAsync();

            // 2) confirma match exato após split (tolerante a separadores)
            var picked = candidates.FirstOrDefault(x =>
                SplitBarCodes(x.d.bar_code).Any(code => string.Equals(code, token, StringComparison.Ordinal)));

            if (picked == null) return new SkuDetailViewModel();

            return new SkuDetailViewModel
            {
                spu_id = picked.m.id,
                spu_code = picked.m.spu_code,
                spu_name = picked.m.spu_name,
                category_id = picked.m.category_id,
                category_name = picked.c.category_name,
                spu_description = picked.m.spu_description,
                supplier_id = picked.m.supplier_id,
                supplier_name = picked.m.supplier_name,
                brand = picked.m.brand,
                origin = picked.m.origin,
                length_unit = picked.m.length_unit,
                volume_unit = picked.m.volume_unit,
                weight_unit = picked.m.weight_unit,
                sku_id = picked.d.id,
                sku_code = picked.d.sku_code,
                sku_name = picked.d.sku_name,
                bar_code = picked.d.bar_code,
                image_url = picked.d.image_url,
                weight = picked.d.weight,
                lenght = picked.d.lenght,
                width = picked.d.width,
                height = picked.d.height,
                volume = picked.d.volume,
                unit = picked.d.unit,
                cost = picked.d.cost,
                price = picked.d.price
            };
        }

        public async Task<(int id, string msg)> AddAsync(SpuBothViewModel viewModel, CurrentUser currentUser)
        {
            var DbSet = _dBContext.GetDbSet<SpuEntity>();
            if (await DbSet.AsNoTracking().AnyAsync(t => t.tenant_id.Equals(currentUser.tenant_id) && t.spu_code.Equals(viewModel.spu_code)))
            {
                return (0, string.Format(_stringLocalizer["exists_entity"], _stringLocalizer["spu_code"], viewModel.spu_code));
            }
            var entity = viewModel.Adapt<SpuEntity>();
            entity.id = 0;
            entity.creator = currentUser.user_name;
            entity.create_time = DateTime.Now;
            entity.last_update_time = DateTime.Now;
            entity.tenant_id = currentUser.tenant_id;

            if (viewModel.detailList.Any())
            {
                decimal dec = ChangeLengthUnit(entity.length_unit, entity.volume_unit);
                viewModel.detailList.ForEach(t =>
                {
                    t.id = 0;
                    t.bar_code = NormalizeBarCode(t.bar_code); // normaliza antes de gravar
                    t.volume = Math.Round(t.lenght * dec * t.width * dec * t.height * dec, 3);
                });
                if (entity.detailList != null)
                {
                    foreach (var d in entity.detailList)
                    {
                        d.bar_code = NormalizeBarCode(d.bar_code);
                    }
                }
            }

            await DbSet.AddAsync(entity);
            await _dBContext.SaveChangesAsync();
            return entity.id > 0
                ? (entity.id, _stringLocalizer["save_success"])
                : (0, _stringLocalizer["save_failed"]);
        }

        public async Task<(int count, string msg)> AddListAsync(List<SpuBothViewModel> viewModels, CurrentUser currentUser)
        {
            if (viewModels == null || !viewModels.Any())
                return (0, _stringLocalizer["batch_empty"]);

            var dbSet = _dBContext.GetDbSet<SpuEntity>();
            var tenantId = currentUser.tenant_id;
            var spuCodes = viewModels.Select(vm => vm.spu_code?.Trim()).ToList();

            var invalidSpuCodes = viewModels.Where(vm => string.IsNullOrWhiteSpace(vm.spu_code?.Trim()))
                                            .Select((vm, idx) => $"第{idx + 1}条数据")
                                            .ToList();
            if (invalidSpuCodes.Any())
                return (0, string.Format(_stringLocalizer["spu_code_required"], string.Join("、", invalidSpuCodes)));

            var duplicateCodes = spuCodes.GroupBy(code => code)
                                         .Where(group => group.Count() > 1 && !string.IsNullOrEmpty(group.Key))
                                         .Select(group => group.Key).ToList();
            if (duplicateCodes.Any())
                return (0, string.Format(_stringLocalizer["batch_duplicate_spu_code"], string.Join(",", duplicateCodes)));

            var existingCodes = await dbSet.AsNoTracking()
                .Where(t => t.tenant_id == tenantId && spuCodes.Contains(t.spu_code))
                .Select(t => t.spu_code)
                .ToListAsync();

            List<SpuBothViewModel> newVms = new();
            List<SpuBothViewModel> existingVms = new();
            List<string> inconsistentCodes = new();
            Dictionary<string, SpuEntity> existingSpuDict = new();

            if (existingCodes.Any())
            {
                existingSpuDict = await dbSet.AsNoTracking()
                    .Include(spu => spu.detailList)
                    .Where(t => t.tenant_id == tenantId && existingCodes.Contains(t.spu_code))
                    .ToDictionaryAsync(s => s.spu_code.Trim(), s => s);

                foreach (var vm in viewModels)
                {
                    var spuCode = vm.spu_code!.Trim();
                    if (existingSpuDict.ContainsKey(spuCode))
                    {
                        var existingSpu = existingSpuDict[spuCode];
                        bool isNameConsistent = string.Equals(vm.spu_name?.Trim(), existingSpu.spu_name?.Trim(), StringComparison.OrdinalIgnoreCase);
                        bool isSupplierConsistent = string.Equals(vm.supplier_name?.Trim(), existingSpu.supplier_name?.Trim(), StringComparison.OrdinalIgnoreCase);

                        if (!isNameConsistent || !isSupplierConsistent)
                            inconsistentCodes.Add(spuCode);
                        else
                            existingVms.Add(vm);
                    }
                    else
                    {
                        newVms.Add(vm);
                    }
                }
                if (inconsistentCodes.Any())
                    return (0, string.Format(_stringLocalizer["spu_info_inconsistent"], string.Join(",", inconsistentCodes)));
            }
            else
            {
                newVms = viewModels.ToList();
            }

            var allVms = newVms.Concat(existingVms).ToList();
            var supplierNames = allVms.Select(vm => vm.supplier_name?.Trim()).Where(name => !string.IsNullOrEmpty(name)).Distinct().ToList();
            var categoryNames = allVms.Select(vm => vm.category_name?.Trim()).Where(name => !string.IsNullOrEmpty(name)).Distinct().ToList();

            var existingSuppliers = await _dBContext.GetDbSet<SupplierEntity>().AsNoTracking()
                .Where(s => s.tenant_id == tenantId && supplierNames.Contains(s.supplier_name))
                .Select(s => new { s.supplier_name, s.id })
                .ToDictionaryAsync(key => key.supplier_name.Trim(), value => value.id);

            var existingCategories = await _dBContext.GetDbSet<CategoryEntity>().AsNoTracking()
                .Where(c => c.tenant_id == tenantId && categoryNames.Contains(c.category_name))
                .Select(c => new { c.category_name, c.id })
                .ToDictionaryAsync(key => key.category_name.Trim(), value => value.id);

            foreach (var vm in allVms)
            {
                var supplierName = vm.supplier_name?.Trim();
                if (string.IsNullOrEmpty(supplierName) || !existingSuppliers.TryGetValue(supplierName, out var supplierId))
                    return (0, string.Format(_stringLocalizer["supplier_not_exists"], supplierName));
                vm.supplier_id = supplierId;

                var categoryName = vm.category_name?.Trim();
                if (string.IsNullOrEmpty(categoryName) || !existingCategories.TryGetValue(categoryName, out var categoryId))
                    return (0, string.Format(_stringLocalizer["category_not_exists"], categoryName));
                vm.category_id = categoryId;
            }

            using var transaction = await _dBContext.Database.BeginTransactionAsync();
            try
            {
                int totalAffected = 0;

                foreach (var vm in existingVms)
                {
                    var spuCode = vm.spu_code!.Trim();
                    var existingSpu = existingSpuDict[spuCode];
                    var skuDbSet = _dBContext.GetDbSet<SkuEntity>();

                    var newSkus = vm.detailList?.Select(skuVm =>
                    {
                        skuVm.bar_code = NormalizeBarCode(skuVm.bar_code);
                        var sku = skuVm.Adapt<SkuEntity>();
                        sku.id = 0;
                        sku.spu_id = existingSpu.id;
                        decimal unitConvert = ChangeLengthUnit(existingSpu.length_unit, existingSpu.volume_unit);
                        sku.volume = Math.Round(sku.lenght * unitConvert * sku.width * unitConvert * sku.height * unitConvert, 3);
                        sku.bar_code = NormalizeBarCode(sku.bar_code);
                        return sku;
                    }).Where(sku => sku != null).ToList();

                    if (newSkus?.Any() ?? false)
                    {
                        var duplicateSkuCodes = newSkus.GroupBy(s => s.sku_code?.Trim())
                                                       .Where(g => g.Count() > 1 && !string.IsNullOrEmpty(g.Key))
                                                       .Select(g => g.Key).ToList();
                        if (duplicateSkuCodes.Any())
                            return (0, string.Format(_stringLocalizer["duplicate_sku_in_batch"], spuCode, string.Join(",", duplicateSkuCodes)));

                        var existingSkuCodes = existingSpu.detailList?
                            .Select(s => s.sku_code?.Trim()).Where(c => !string.IsNullOrEmpty(c)).ToList() ?? new List<string>();

                        var conflictSkuCodes = newSkus
                            .Where(s => !string.IsNullOrEmpty(s.sku_code?.Trim()) && existingSkuCodes.Contains(s.sku_code.Trim()))
                            .Select(s => s.sku_code!.Trim()).ToList();

                        if (conflictSkuCodes.Any())
                            return (0, string.Format(_stringLocalizer["sku_code_exists"], spuCode, string.Join(",", conflictSkuCodes)));

                        await skuDbSet.AddRangeAsync(newSkus);
                        existingSpu.last_update_time = DateTime.Now;
                        dbSet.Update(existingSpu);

                        totalAffected += newSkus.Count;
                    }
                }

                if (newVms.Any())
                {
                    var newSpuEntities = newVms.Select(vm =>
                    {
                        if (vm.detailList != null)
                        {
                            foreach (var d in vm.detailList)
                                d.bar_code = NormalizeBarCode(d.bar_code);
                        }

                        var entity = vm.Adapt<SpuEntity>();
                        entity.id = 0;
                        entity.creator = currentUser.user_name;
                        entity.create_time = DateTime.Now;
                        entity.last_update_time = DateTime.Now;
                        entity.tenant_id = tenantId;

                        if (entity.detailList?.Any() ?? false)
                        {
                            decimal unitConvert = ChangeLengthUnit(entity.length_unit, entity.volume_unit);
                            foreach (var sku in entity.detailList)
                            {
                                sku.id = 0;
                                sku.volume = Math.Round(sku.lenght * unitConvert * sku.width * unitConvert * sku.height * unitConvert, 3);
                                sku.bar_code = NormalizeBarCode(sku.bar_code);
                            }
                        }
                        return entity;
                    }).ToList();

                    await dbSet.AddRangeAsync(newSpuEntities);
                    totalAffected += newSpuEntities.Count;
                }

                await _dBContext.SaveChangesAsync();
                await transaction.CommitAsync();
                return (totalAffected, _stringLocalizer["batch_save_success"]);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (0, string.Format(_stringLocalizer["batch_save_failed"], ex.Message));
            }
        }

        public async Task<(string url, string msg)> UploadImg(IFormFile img, CurrentUser currentUser)
        {
            if (img == null || img.Length == 0)
                return (string.Empty, _stringLocalizer["img_required"]);
            try
            {
                var allowedContentTypes = new[] { "image/jpeg", "image/png", "image/gif", "image/bmp" };
                if (!allowedContentTypes.Contains(img.ContentType))
                    return (string.Empty, _stringLocalizer["Unsupported image types, only JPG, PNG, GIF, BMP formats are allowed"]);

                var maxFileSize = 5 * 1024 * 1024;
                if (img.Length > maxFileSize)
                    return (string.Empty, string.Format(_stringLocalizer["The size of the image cannot be exceeded{0}MB"], maxFileSize / 1024 / 1024));

                var uploadDir = GetImageStorageDirectory();
                if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

                var fileExtension = Path.GetExtension(img.FileName).ToLowerInvariant();
                var uniqueFileName = $"sku-img_{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(uploadDir, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await img.CopyToAsync(stream);
                }
                var imageUrl = $"/{string.Join("/", _imageRelativePath)}/{uniqueFileName}";
                return (imageUrl, _stringLocalizer["Upload sucess"]);
            }
            catch (IOException)
            {
                return (string.Empty, _stringLocalizer["Upload failed"]);
            }
            catch (Exception ex)
            {
                return (string.Empty, string.Format(_stringLocalizer["Upload failed: {0}"], ex.Message));
            }
        }

        public async Task<(bool flag, string msg)> DeleteImg(string imageUrl, CurrentUser currentUser)
        {
            if (string.IsNullOrEmpty(imageUrl))
                return (false, _stringLocalizer["image_url_required"]);
            try
            {
                var fileName = GetFileNameFromUrl(imageUrl);
                if (!IsValidImageFileName(fileName))
                    return (false, _stringLocalizer["invalid_image_url"]);

                var uploadDir = GetImageStorageDirectory();
                var filePath = Path.Combine(uploadDir, fileName);
                if (!File.Exists(filePath))
                    return (false, _stringLocalizer["image_file_not_found"]);

                File.Delete(filePath);

                var skuEntity = await _dBContext.GetDbSet<SkuEntity>()
                    .FirstOrDefaultAsync(s => s.image_url == imageUrl);
                if (skuEntity != null)
                {
                    skuEntity.image_url = null;
                    skuEntity.last_update_time = DateTime.Now;
                    await _dBContext.SaveChangesAsync();
                }
                return (true, _stringLocalizer["image_delete_success"]);
            }
            catch (IOException)
            {
                return (false, _stringLocalizer["image_delete_failed_file_in_use"]);
            }
            catch (Exception ex)
            {
                return (false, string.Format(_stringLocalizer["image_delete_failed: {0}"], ex.Message));
            }
        }

        private decimal ChangeLengthUnit(byte length_unit, byte volume_unit)
        {
            if (volume_unit.Equals(0)) // cm3
            {
                if (length_unit.Equals(0)) return 0.1M;   // mm
                else if (length_unit.Equals(2)) return 10M; // dm
                else if (length_unit.Equals(3)) return 100M; // m
                else return 1M; // cm
            }
            else if (volume_unit.Equals(1)) // dm3
            {
                if (length_unit.Equals(0)) return 0.01M;
                else if (length_unit.Equals(2)) return 1M;
                else if (length_unit.Equals(3)) return 10M;
                else return 0.1M;
            }
            else if (volume_unit.Equals(2)) // m3
            {
                if (length_unit.Equals(0)) return 0.001M;
                else if (length_unit.Equals(2)) return 0.1M;
                else if (length_unit.Equals(3)) return 1M;
                else return 0.01M;
            }
            else
            {
                return 1M;
            }
        }

        private async Task<SpuEntity?> GetSpuEntityAsync(int id)
        {
            var Spus = _dBContext.GetDbSet<SpuEntity>();
            var Skus = _dBContext.GetDbSet<SkuEntity>();
            var SkuSafetyStocks = _dBContext.GetDbSet<SkuSafetyStockEntity>();
            var entity = await (
                 from m in Spus
                 where m.id == id
                 select new SpuEntity
                 {
                     id = m.id,
                     spu_code = m.spu_code,
                     spu_name = m.spu_name,
                     category_id = m.category_id,
                     spu_description = m.spu_description,
                     supplier_id = m.supplier_id,
                     supplier_name = m.supplier_name,
                     brand = m.brand,
                     origin = m.origin,
                     length_unit = m.length_unit,
                     volume_unit = m.volume_unit,
                     weight_unit = m.weight_unit,
                     creator = m.creator,
                     create_time = m.create_time,
                     last_update_time = m.last_update_time,
                     is_valid = m.is_valid,
                     detailList = Skus.Where(t => t.spu_id.Equals(m.id))
                                  .Select(t => new SkuEntity
                                  {
                                      Spu = m,
                                      id = t.id,
                                      spu_id = t.spu_id,
                                      sku_code = t.sku_code,
                                      sku_name = t.sku_name,
                                      bar_code = t.bar_code,
                                      weight = t.weight,
                                      lenght = t.lenght,
                                      width = t.width,
                                      height = t.height,
                                      volume = t.volume,
                                      unit = t.unit,
                                      cost = t.cost,
                                      price = t.price,
                                      create_time = t.create_time,
                                      last_update_time = t.last_update_time,
                                      detailList = (from sss in SkuSafetyStocks
                                                    where sss.sku_id.Equals(t.id)
                                                    select new SkuSafetyStockEntity
                                                    {
                                                        Sku = t,
                                                        id = sss.id,
                                                        sku_id = sss.sku_id,
                                                        safety_stock_qty = sss.safety_stock_qty,
                                                        warehouse_id = sss.warehouse_id
                                                    }).ToList()
                                  }).ToList()
                 }).FirstOrDefaultAsync();
            return entity;
        }

        public async Task<(bool flag, string msg)> UpdateAsync(SpuBothViewModel viewModel)
        {
            var DbSet = _dBContext.GetDbSet<SpuEntity>();
            var entity = await DbSet.Include(d => d.detailList)
                .FirstOrDefaultAsync(t => t.id.Equals(viewModel.id));
            if (entity == null) return (false, _stringLocalizer["not_exists_entity"]);

            if (await DbSet.AsNoTracking().AnyAsync(t => !t.id.Equals(viewModel.id) && t.tenant_id.Equals(entity.tenant_id) && t.spu_code.Equals(viewModel.spu_code)))
                return (false, string.Format(_stringLocalizer["exists_entity"], _stringLocalizer["spu_code"], viewModel.spu_code));

            entity.spu_code = viewModel.spu_code;
            entity.spu_name = viewModel.spu_name;
            entity.category_id = viewModel.category_id;
            entity.spu_description = viewModel.spu_description;
            entity.supplier_id = viewModel.supplier_id;
            entity.supplier_name = viewModel.supplier_name;
            entity.brand = viewModel.brand;
            entity.origin = viewModel.origin;
            entity.length_unit = viewModel.length_unit;
            entity.volume_unit = viewModel.volume_unit;
            entity.weight_unit = viewModel.weight_unit;
            entity.is_valid = viewModel.is_valid;
            entity.last_update_time = DateTime.Now;

            if (viewModel.detailList.Any(t => t.id > 0))
            {
                entity.detailList.ForEach(d =>
                {
                    var vm = viewModel.detailList.Where(t => t.id > 0).FirstOrDefault(t => t.id == d.id);
                    if (vm != null)
                    {
                        d.sku_code = vm.sku_code;
                        d.sku_name = vm.sku_name;
                        d.bar_code = NormalizeBarCode(vm.bar_code);
                        d.image_url = vm.image_url;
                        d.weight = vm.weight;
                        d.lenght = vm.lenght;
                        d.width = vm.width;
                        d.height = vm.height;
                        d.volume = vm.volume;
                        d.unit = vm.unit;
                        d.cost = vm.cost;
                        d.price = vm.price;
                        d.last_update_time = DateTime.Now;
                    }
                });
            }
            if (viewModel.detailList.Any(t => t.id == 0))
            {
                var toAdd = viewModel.detailList.Where(t => t.id == 0).ToList();
                foreach (var it in toAdd) it.bar_code = NormalizeBarCode(it.bar_code);
                entity.detailList.AddRange(toAdd.Adapt<List<SkuEntity>>());
            }
            if (viewModel.detailList.Any(t => t.id < 0))
            {
                var delIds = viewModel.detailList.Where(t => t.id < 0).Select(t => t.id * -1).ToList();
                entity.detailList.RemoveAll(e => delIds.Contains(e.id));
            }

            var qty = await _dBContext.SaveChangesAsync();
            if (qty > 0)
            {
                decimal dec = ChangeLengthUnit(entity.length_unit, entity.volume_unit);
                await _dBContext.GetDbSet<SkuEntity>().Where(t => t.spu_id.Equals(entity.id))
                    .ExecuteUpdateAsync(p => p.SetProperty(x => x.volume, x => Math.Round(x.lenght * dec * x.width * dec * x.height * dec, 3)));
                return (true, _stringLocalizer["save_success"]);
            }
            return (false, _stringLocalizer["save_failed"]);
        }

        public async Task<(bool flag, string msg)> DeleteAsync(int id)
        {
            var Asns = _dBContext.GetDbSet<AsnEntity>();
            if (await Asns.AsNoTracking().AnyAsync(t => t.spu_id.Equals(id)))
                return (false, _stringLocalizer["delete_referenced"]);

            var qty = await _dBContext.GetDbSet<SkuEntity>().Where(t => t.spu_id.Equals(id)).ExecuteDeleteAsync();
            qty += await _dBContext.GetDbSet<SpuEntity>().Where(t => t.id.Equals(id)).ExecuteDeleteAsync();
            return qty > 0
                ? (true, _stringLocalizer["delete_success"])
                : (false, _stringLocalizer["delete_failed"]);
        }
        #endregion

        #region add or update sku_safety_stock
        public async Task<(bool flag, string msg)> InsertOrUpdateSkuSafetyStockAsync(SkuSafetyStockPutViewModel viewModel)
        {
            if (!viewModel.detailList.Any()) return (false, _stringLocalizer["save_failed"]);

            var SafetyDB = _dBContext.GetDbSet<SkuSafetyStockEntity>();
            var entities = await SafetyDB.Where(t => t.sku_id == viewModel.sku_id).ToListAsync();

            viewModel.detailList.ForEach(async t =>
            {
                if (t.id == 0)
                {
                    await SafetyDB.AddAsync(new SkuSafetyStockEntity { sku_id = viewModel.sku_id, safety_stock_qty = t.safety_stock_qty, warehouse_id = t.warehouse_id });
                }
                else
                {
                    var ent = entities.FirstOrDefault(e => e.id == Math.Abs(t.id));
                    if (ent != null)
                    {
                        if (t.id < 0) SafetyDB.Remove(ent);
                        else
                        {
                            ent.warehouse_id = t.warehouse_id;
                            ent.safety_stock_qty = t.safety_stock_qty;
                        }
                    }
                }
            });

            await _dBContext.SaveChangesAsync();
            return (true, _stringLocalizer["save_success"]);
        }
        #endregion
    }
}
