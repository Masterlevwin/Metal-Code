using Metal_Code.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Metal_Code.Services
{
    /// <summary>
    /// Сервис для работы с гибридной базой данных: PostgreSQL (основная) + SQLite (локальная).
    /// При наличии сети работает ТОЛЬКО с PG, при её отсутствии — с локальной SQLite.
    /// Локальная SQLite используется как буфер для оффлайн-работы.
    /// </summary>
    public class HybridDataService
    {
        private readonly DbContextOptions<AppDbContext> _pgOptions;
        private readonly string[] _connections;

        private bool _isOnline;
        public bool IsOnline => _isOnline;

        public HybridDataService(DbContextOptions<AppDbContext> pgOptions, string[] connections)
        {
            _pgOptions = pgOptions;
            _connections = connections;
        }

        public async Task<bool> InitializeAsync()
        {
            if (IsPostgresDisabledByConfig())
            {
                _isOnline = false;
                Trace.WriteLine("🔌 PostgreSQL отключён через конфигурацию (DisablePostgres=true)");
                return false;
            }

            try
            {
                using var testCtx = new AppDbContext(_pgOptions);
                testCtx.Database.SetCommandTimeout(5);
                await testCtx.Database.ExecuteSqlRawAsync("SELECT 1");

                _isOnline = true;
                Trace.WriteLine("✅ PG доступен");
                await SyncReferenceDataAsync();
                return true;
            }
            catch (Exception ex)
            {
                _isOnline = false;
                Trace.WriteLine($"⚠️ PG недоступен: {ex.Message}");
                return false;
            }
        }

        private bool IsPostgresDisabledByConfig()
        {
            try
            {
                string? setting = System.Configuration.ConfigurationManager.AppSettings["DisablePostgres"];
                if (bool.TryParse(setting, out bool disabled))
                    return disabled;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"⚠️ Ошибка чтения настройки DisablePostgres: {ex.Message}");
            }
            return false;
        }

        public async System.Threading.Tasks.Task SyncReferenceDataAsync()
        {
            if (!_isOnline) return;
            try
            {
                using var pgContext = new AppDbContext(_pgOptions);

                var pgMetals = await pgContext.Metals.AsNoTracking().ToListAsync();
                if (pgMetals.Any())
                {
                    using var localCtx = new MetalContext(_connections[3]);
                    localCtx.Metals.RemoveRange(localCtx.Metals);
                    localCtx.Metals.AddRange(pgMetals);
                    await localCtx.SaveChangesAsync();
                }

                var pgTypes = await pgContext.TypeDetails.AsNoTracking().ToListAsync();
                if (pgTypes.Any())
                {
                    using var localCtx = new TypeDetailContext(_connections[1]);
                    localCtx.TypeDetails.RemoveRange(localCtx.TypeDetails);
                    localCtx.TypeDetails.AddRange(pgTypes);
                    await localCtx.SaveChangesAsync();
                }

                var pgWorks = await pgContext.Works.AsNoTracking().ToListAsync();
                if (pgWorks.Any())
                {
                    using var localCtx = new WorkContext(_connections[2]);
                    localCtx.Works.RemoveRange(localCtx.Works);
                    localCtx.Works.AddRange(pgWorks);
                    await localCtx.SaveChangesAsync();
                }
            }
            catch { }
        }

        public async System.Threading.Tasks.Task SyncManagersAsync(bool isEngineerOrAdmin)
        {
            if (!_isOnline) return;
            try
            {
                using var pgContext = new AppDbContext(_pgOptions);
                using var localCtx = new ManagerContext(_connections[0]);

                var localCurrentUser = await localCtx.Managers
                    .FirstOrDefaultAsync(m => m.MachineName == Environment.MachineName);

                if (localCurrentUser == null)
                {
                    localCurrentUser = await localCtx.Managers
                        .FirstOrDefaultAsync(m => m.Contact == Environment.MachineName);
                    if (localCurrentUser != null)
                    {
                        localCurrentUser.MachineName = Environment.MachineName;
                        await localCtx.SaveChangesAsync();
                    }
                }

                if (localCurrentUser != null)
                {
                    var pgCurrentUser = await pgContext.Managers
                        .FirstOrDefaultAsync(m => m.Name == localCurrentUser.Name);
                    if (pgCurrentUser != null)
                    {
                        if (string.IsNullOrEmpty(pgCurrentUser.MachineName))
                        {
                            pgCurrentUser.MachineName = localCurrentUser.MachineName;
                            await pgContext.SaveChangesAsync();
                        }
                        localCurrentUser.IsAdmin = pgCurrentUser.IsAdmin;
                        localCurrentUser.IsEngineer = pgCurrentUser.IsEngineer;
                        localCurrentUser.IsLaser = pgCurrentUser.IsLaser;
                        await localCtx.SaveChangesAsync();
                    }
                }

                var pgAllManagers = await pgContext.Managers.AsNoTracking().ToListAsync();
                var localAllManagers = await localCtx.Managers.ToListAsync();

                foreach (var pgManager in pgAllManagers)
                {
                    var localManager = localAllManagers.FirstOrDefault(m => m.Name == pgManager.Name);
                    if (localManager != null)
                    {
                        localManager.IsAdmin = pgManager.IsAdmin;
                        localManager.IsEngineer = pgManager.IsEngineer;
                        localManager.IsLaser = pgManager.IsLaser;
                        localCtx.Managers.Update(localManager);
                    }
                    else if (isEngineerOrAdmin)
                    {
                        localCtx.Managers.Add(new Manager
                        {
                            Name = pgManager.Name,
                            MachineName = pgManager.MachineName,
                            Contact = pgManager.Contact,
                            IsAdmin = pgManager.IsAdmin,
                            IsEngineer = pgManager.IsEngineer,
                            IsLaser = pgManager.IsLaser,
                            Password = pgManager.Password
                        });
                    }
                }
                await localCtx.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Ошибка синхронизации менеджеров: {ex.Message}");
            }
        }

        public async Task<int> SyncPendingOffersAsync()
        {
            if (!_isOnline) return 0;
            try
            {
                using var localCtx = new ManagerContext(_connections[0]);
                var pendingOffers = await localCtx.Offers
                    .AsNoTracking()
                    .Where(o => o.IsPendingSync)
                    .ToListAsync();

                if (!pendingOffers.Any())
                {
                    Trace.WriteLine("ℹ️ Отложенных расчётов нет");
                    return 0;
                }

                Trace.WriteLine($"🔄 Синхронизация {pendingOffers.Count} отложенных расчётов...");
                int syncedCount = 0;

                using var pgContext = new AppDbContext(_pgOptions);
                pgContext.Database.SetCommandTimeout(60);

                foreach (var localOffer in pendingOffers)
                {
                    try
                    {
                        var localManager = await localCtx.Managers.AsNoTracking()
                            .FirstOrDefaultAsync(m => m.Id == localOffer.ManagerId);
                        if (localManager == null) continue;

                        // Пропускаем расчетного менеджера
                        if (localManager.Name == "Расчетный менеджер"
                            && string.IsNullOrWhiteSpace(localManager.MachineName)
                            && string.IsNullOrWhiteSpace(localManager.Contact))
                        {
                            continue;
                        }

                        var pgManager = await pgContext.Managers.AsNoTracking()
                            .FirstOrDefaultAsync(m => m.Name == localManager.Name);
                        if (pgManager == null) continue;

                        var existingPgOffer = await pgContext.Offers.AsNoTracking()
                            .FirstOrDefaultAsync(o => o.N == localOffer.N && o.ManagerId == pgManager.Id);

                        if (existingPgOffer == null)
                        {
                            var pgOffer = new Offer
                            {
                                N = localOffer.N,
                                Company = localOffer.Company,
                                Amount = localOffer.Amount,
                                Material = localOffer.Material,
                                Services = localOffer.Services,
                                Agent = localOffer.Agent,
                                Invoice = localOffer.Invoice,
                                CreatedDate = localOffer.CreatedDate.HasValue
                                    ? DateTime.SpecifyKind(localOffer.CreatedDate.Value, DateTimeKind.Utc)
                                    : DateTime.UtcNow,
                                EndDate = localOffer.EndDate.HasValue
                                    ? DateTime.SpecifyKind(localOffer.EndDate.Value, DateTimeKind.Utc) : null,
                                Order = localOffer.Order,
                                Autor = localOffer.Autor,
                                Act = localOffer.Act,
                                ManagerId = pgManager.Id,
                                Data = localOffer.Data
                            };
                            pgContext.Offers.Add(pgOffer);
                            await pgContext.SaveChangesAsync();
                        }

                        // Удаляем локальный расчёт
                        await localCtx.Database.ExecuteSqlInterpolatedAsync(
                            $"DELETE FROM Offers WHERE Id = {localOffer.Id}");
                        syncedCount++;
                    }
                    catch (Exception ex)
                    {
                        Trace.WriteLine($"❌ Ошибка синхронизации расчёта {localOffer.N}: {ex.Message}");
                    }
                }

                Trace.WriteLine($"✅ Синхронизировано расчётов: {syncedCount} из {pendingOffers.Count}");
                return syncedCount;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"❌ Ошибка SyncPendingOffersAsync: {ex.Message}");
                return 0;
            }
        }

        public async System.Threading.Tasks.Task MigrateUserDataToPgAsync()
        {
            if (!_isOnline) return;
            try
            {
                using var pgContext = new AppDbContext(_pgOptions);
                using var localCtx = new ManagerContext(_connections[0]);

                await pgContext.Database.ExecuteSqlRawAsync(@"
            SELECT setval('managers_id_seq', COALESCE((SELECT MAX(id) FROM managers), 1));
            SELECT setval('offers_id_seq', COALESCE((SELECT MAX(id) FROM offers), 1));
            SELECT setval('customers_id_seq', COALESCE((SELECT MAX(id) FROM customers), 1));
        ");

                var localCurrentUser = await localCtx.Managers.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.MachineName == Environment.MachineName || m.Contact == Environment.MachineName);
                if (localCurrentUser == null) return;

                var pgCurrentUser = await pgContext.Managers.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Name == localCurrentUser.Name);
                if (pgCurrentUser == null) return;

                if (string.IsNullOrEmpty(pgCurrentUser.MachineName) && !string.IsNullOrEmpty(localCurrentUser.MachineName))
                {
                    var trackedUser = await pgContext.Managers.FirstOrDefaultAsync(m => m.Id == pgCurrentUser.Id);
                    if (trackedUser != null)
                    {
                        trackedUser.MachineName = localCurrentUser.MachineName;
                        await pgContext.SaveChangesAsync();
                    }
                }

                List<string> managersToMigrate = new();
                if (localCurrentUser.IsEngineer || localCurrentUser.IsAdmin)
                {
                    List<Manager> pgManagers = await pgContext.Managers.AsNoTracking().ToListAsync();
                    managersToMigrate = pgManagers.Where(m => m.Name != null).Select(m => m.Name!).ToList();
                }
                else if (localCurrentUser.Name != null)
                {
                    managersToMigrate = new List<string> { localCurrentUser.Name };
                }

                foreach (var managerName in managersToMigrate)
                {
                    var pgManager = await pgContext.Managers.AsNoTracking().FirstOrDefaultAsync(m => m.Name == managerName);
                    if (pgManager == null) continue;

                    // Пропускаем расчетного менеджера
                    if (string.IsNullOrWhiteSpace(pgManager.MachineName)
                        && string.IsNullOrWhiteSpace(pgManager.Contact))
                    {
                        continue;
                    }

                    var localIds = await localCtx.Managers.AsNoTracking()
                        .Where(m => m.Name == managerName).Select(m => m.Id).ToListAsync();
                    if (!localIds.Any()) continue;

                    // Миграция заказчиков
                    var localCustomersCount = await localCtx.Customers.AsNoTracking()
                        .Where(c => localIds.Contains(c.ManagerId)).CountAsync();
                    var pgCustomersCount = await pgContext.Customers.AsNoTracking()
                        .Where(c => c.ManagerId == pgManager.Id).CountAsync();

                    if (pgCustomersCount < localCustomersCount)
                    {
                        var localCustomers = await localCtx.Customers.AsNoTracking()
                            .Where(c => localIds.Contains(c.ManagerId)).ToListAsync();
                        var pgCustomerNames = await pgContext.Customers.AsNoTracking()
                            .Where(c => c.ManagerId == pgManager.Id).Select(c => c.Name).ToListAsync();
                        var customersToMigrate = localCustomers
                            .Where(c => !pgCustomerNames.Contains(c.Name)).ToList();

                        if (customersToMigrate.Any())
                        {
                            foreach (var c in customersToMigrate)
                            {
                                string specTemplateJson = System.Text.Json.JsonSerializer.Serialize(c.SpecTemplate);
                                pgContext.Customers.Add(new Customer
                                {
                                    Name = c.Name,
                                    Address = c.Address,
                                    Agent = c.Agent,
                                    DeliveryPrice = c.DeliveryPrice,
                                    ManagerId = pgManager.Id,
                                    SpecTemplateJson = specTemplateJson
                                });
                            }
                            await pgContext.SaveChangesAsync();
                            Trace.WriteLine($"✅ Мигрировано {customersToMigrate.Count} заказчиков для '{managerName}'");
                        }
                    }

                    // Миграция расчётов
                    var pgOfferNumbers = await pgContext.Offers.AsNoTracking()
                        .Where(o => o.ManagerId == pgManager.Id).Select(o => o.N).ToListAsync();
                    var localOffers = await localCtx.Offers.AsNoTracking()
                        .Where(o => localIds.Contains(o.ManagerId)).ToListAsync();
                    var missingOfferNumbers = localOffers.Select(o => o.N)
                        .Where(n => !pgOfferNumbers.Contains(n)).ToList();

                    if (missingOfferNumbers.Any())
                    {
                        var offersToMigrate = localOffers.Where(o => missingOfferNumbers.Contains(o.N)).ToList();
                        int savedCount = 0, failedCount = 0;

                        foreach (var o in offersToMigrate)
                        {
                            try
                            {
                                var pgOffer = new Offer
                                {
                                    N = o.N,
                                    Company = o.Company,
                                    Amount = o.Amount,
                                    Material = o.Material,
                                    Services = o.Services,
                                    Agent = o.Agent,
                                    Invoice = o.Invoice,
                                    CreatedDate = o.CreatedDate.HasValue
                                        ? DateTime.SpecifyKind(o.CreatedDate.Value, DateTimeKind.Utc)
                                        : DateTime.UtcNow,
                                    EndDate = o.EndDate.HasValue
                                        ? DateTime.SpecifyKind(o.EndDate.Value, DateTimeKind.Utc) : null,
                                    Order = o.Order,
                                    Autor = o.Autor,
                                    Act = o.Act,
                                    ManagerId = pgManager.Id,
                                    Data = o.Data
                                };
                                pgContext.Offers.Add(pgOffer);
                                await pgContext.SaveChangesAsync();

                                await localCtx.Database.ExecuteSqlInterpolatedAsync(
                                    $"DELETE FROM Offers WHERE Id = {o.Id}");
                                savedCount++;
                            }
                            catch (Exception ex)
                            {
                                failedCount++;
                                Trace.WriteLine($"⚠️ Пропуск расчёта {o.N}: {ex.Message}");
                            }
                        }

                        foreach (var entry in pgContext.ChangeTracker.Entries().ToList())
                            entry.State = EntityState.Detached;

                        Trace.WriteLine($"✅ Мигрировано {savedCount} расчётов для '{managerName}'" +
                                        (failedCount > 0 ? $" (ошибок: {failedCount})" : ""));
                    }

                    // Синхронизация ролей
                    var pgManagersList = await pgContext.Managers.AsNoTracking().ToListAsync();
                    foreach (var pgMgr in pgManagersList)
                    {
                        await localCtx.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE Managers 
                    SET IsAdmin = {pgMgr.IsAdmin}, IsEngineer = {pgMgr.IsEngineer}, IsLaser = {pgMgr.IsLaser}
                    WHERE Name = {pgMgr.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"❌ Ошибка миграции: {ex.Message}");
            }
        }

        public async System.Threading.Tasks.Task CleanupOrphanedLocalOffersAsync()
        {
            if (!_isOnline) return;
            try
            {
                using var localCtx = new ManagerContext(_connections[0]);
                using var pgContext = new AppDbContext(_pgOptions);

                var localManagers = await localCtx.Managers.AsNoTracking().ToListAsync();
                int totalDeleted = 0;

                foreach (var localManager in localManagers)
                {
                    // Пропускаем расчетного менеджера
                    if (string.IsNullOrWhiteSpace(localManager.MachineName)
                        && string.IsNullOrWhiteSpace(localManager.Contact))
                    {
                        continue;
                    }

                    var pgManager = await pgContext.Managers.AsNoTracking()
                        .FirstOrDefaultAsync(m => m.Name == localManager.Name);
                    if (pgManager == null) continue;

                    var pgOfferNumbers = await pgContext.Offers.AsNoTracking()
                        .Where(o => o.ManagerId == pgManager.Id).Select(o => o.N).ToListAsync();
                    var pgNumbersSet = new HashSet<string>(pgOfferNumbers.Where(n => n != null)!);

                    var localOffers = await localCtx.Offers
                        .Where(o => o.ManagerId == localManager.Id && !o.IsPendingSync).ToListAsync();

                    var orphaned = localOffers
                        .Where(o => !string.IsNullOrEmpty(o.N) && !pgNumbersSet.Contains(o.N)).ToList();

                    if (orphaned.Any())
                    {
                        foreach (var offer in orphaned)
                            await localCtx.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM Offers WHERE Id = {offer.Id}");
                        totalDeleted += orphaned.Count;
                    }
                }

                if (totalDeleted > 0)
                    Trace.WriteLine($"🧹 Удалено осиротевших расчётов: {totalDeleted}");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"⚠️ Ошибка очистки осиротевших расчётов: {ex.Message}");
            }
        }

        public async System.Threading.Tasks.Task CleanupLocalOffersAsync()
        {
            try
            {
                using var localCtx = new ManagerContext(_connections[0]);
                localCtx.Database.SetCommandTimeout(30);
                DateTime cutoffDate = DateTime.UtcNow.AddDays(-60);

                var draftManagerIds = await localCtx.Managers.AsNoTracking()
                    .Where(m => string.IsNullOrWhiteSpace(m.MachineName)
                             && string.IsNullOrWhiteSpace(m.Contact))
                    .Select(m => m.Id)
                    .ToListAsync();

                var offersToDelete = await localCtx.Offers
                    .Where(o => o.CreatedDate.HasValue && o.CreatedDate.Value < cutoffDate
                        && (o.Order == null || o.Order == "")
                        && !o.IsPendingSync
                        && !draftManagerIds.Contains(o.ManagerId))
                    .ToListAsync();

                if (!offersToDelete.Any()) return;

                int count = offersToDelete.Count;
                localCtx.Offers.RemoveRange(offersToDelete);
                await localCtx.SaveChangesAsync();
                await localCtx.Database.ExecuteSqlRawAsync("VACUUM");
                Trace.WriteLine($"✅ Очистка локальной БД: удалено {count} расчётов");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"❌ Ошибка очистки локальной БД: {ex.Message}");
            }
        }

        public async Task<List<Manager>> GetLocalManagersAsync()
        {
            using var ctx = new ManagerContext(_connections[0]);
            return await ctx.Managers.ToListAsync();
        }

        public async Task<List<Metal>> GetLocalMetalsAsync()
        {
            using var ctx = new MetalContext(_connections[3]);
            return await ctx.Metals.OrderBy(m => m.Name).ToListAsync();
        }

        public async Task<List<TypeDetail>> GetLocalTypeDetailsAsync()
        {
            using var ctx = new TypeDetailContext(_connections[1]);
            return await ctx.TypeDetails.OrderBy(t => t.Sort).ThenBy(t => t.Name).ToListAsync();
        }

        public async Task<List<Work>> GetLocalWorksAsync()
        {
            using var ctx = new WorkContext(_connections[2]);
            return await ctx.Works.OrderBy(w => w.Name).ToListAsync();
        }

        public async Task<List<Customer>> GetCustomersAsync(int localManagerId, string managerName)
        {
            if (_isOnline)
            {
                try
                {
                    using var pgContext = new AppDbContext(_pgOptions);
                    pgContext.Database.SetCommandTimeout(10);
                    var pgManager = await pgContext.Managers.AsNoTracking()
                        .FirstOrDefaultAsync(m => m.Name == managerName);

                    if (pgManager != null)
                    {
                        using var localCtxForId = new ManagerContext(_connections[0]);
                        var correctLocalManager = await localCtxForId.Managers.AsNoTracking()
                            .FirstOrDefaultAsync(m => m.Name == managerName);
                        int finalLocalManagerId = correctLocalManager?.Id ?? localManagerId;

                        var customers = await pgContext.Customers.AsNoTracking()
                            .Where(c => c.ManagerId == pgManager.Id)
                            .OrderBy(c => c.Name)
                            .Select(c => new Customer
                            {
                                Id = c.Id,
                                Name = c.Name,
                                Address = c.Address,
                                Agent = c.Agent,
                                DeliveryPrice = c.DeliveryPrice,
                                ManagerId = finalLocalManagerId,
                                SpecTemplateJson = c.SpecTemplateJson
                            }).ToListAsync();
                        return customers;
                    }
                }
                catch { _isOnline = false; }
            }

            using var localCtx = new ManagerContext(_connections[0]);
            localCtx.Database.SetCommandTimeout(10);
            var localCustomers = await localCtx.Customers.AsNoTracking()
                .Where(c => c.ManagerId == localManagerId).OrderBy(c => c.Name).ToListAsync();

            foreach (var c in localCustomers)
            {
                if (c.SpecTemplate == null) c.SpecTemplate = new SpecTemplate();
            }
            return localCustomers;
        }

        public async Task<List<Offer>> GetOffersAsync(string managerName, int count = 50)
        {
            // Для расчетного менеджера ВСЕГДА загружаем только из локалки
            if (managerName == "Расчетный менеджер" || !_isOnline)
            {
                using var localCtx = new ManagerContext(_connections[0]);
                localCtx.Database.SetCommandTimeout(10);

                var localManager = await localCtx.Managers.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Name == managerName);
                if (localManager == null) return new List<Offer>();

                var localOffers = await localCtx.Offers.AsNoTracking()
                    .Where(o => o.ManagerId == localManager.Id)
                    .OrderByDescending(o => o.Id).Take(count)
                    .Select(o => new Offer
                    {
                        Id = o.Id,
                        N = o.N,
                        Company = o.Company,
                        Amount = o.Amount,
                        Material = o.Material,
                        Services = o.Services,
                        Agent = o.Agent,
                        Invoice = o.Invoice,
                        CreatedDate = o.CreatedDate,
                        EndDate = o.EndDate,
                        Order = o.Order,
                        Autor = o.Autor,
                        Act = o.Act,
                        ManagerId = o.ManagerId,
                        IsPendingSync = o.IsPendingSync,
                        IsLocalOffer = true
                    }).ToListAsync();

                localOffers.Reverse();
                return localOffers;
            }

            // Обычный менеджер онлайн → загружаем из PG
            try
            {
                using var pgContext = new AppDbContext(_pgOptions);
                pgContext.Database.SetCommandTimeout(10);
                var pgManager = await pgContext.Managers.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Name == managerName);

                if (pgManager != null)
                {
                    using var localCtx = new ManagerContext(_connections[0]);
                    var localManager = await localCtx.Managers.AsNoTracking()
                        .FirstOrDefaultAsync(m => m.Name == managerName);
                    int localManagerIdForOffers = localManager?.Id ?? 0;

                    var pgOffers = await pgContext.Offers.AsNoTracking()
                        .Where(o => o.ManagerId == pgManager.Id)
                        .OrderByDescending(o => o.Id).Take(count)
                        .Select(o => new Offer
                        {
                            Id = o.Id,
                            N = o.N,
                            Company = o.Company,
                            Amount = o.Amount,
                            Material = o.Material,
                            Services = o.Services,
                            Agent = o.Agent,
                            Invoice = o.Invoice,
                            CreatedDate = o.CreatedDate,
                            EndDate = o.EndDate,
                            Order = o.Order,
                            Autor = o.Autor,
                            Act = o.Act,
                            ManagerId = localManagerIdForOffers,
                            IsPendingSync = false,
                            IsLocalOffer = false
                        }).ToListAsync();

                    pgOffers.Reverse();
                    return pgOffers;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"⚠️ Ошибка загрузки из PG: {ex.Message}");
                _isOnline = false;
            }

            return new List<Offer>();
        }

        public async Task<Offer?> LoadOfferDataAsync(int offerId)
        {
            if (_isOnline)
            {
                try
                {
                    using var pgContext = new AppDbContext(_pgOptions);
                    pgContext.Database.SetCommandTimeout(30);
                    var offer = await pgContext.Offers.AsNoTracking().FirstOrDefaultAsync(o => o.Id == offerId);
                    if (offer != null) return offer;
                }
                catch { _isOnline = false; }
            }

            // Фолбэк на локалку — для отложенных расчётов (IsPendingSync = true)
            using var localCtx = new ManagerContext(_connections[0]);
            localCtx.Database.SetCommandTimeout(30);
            return await localCtx.Offers.AsNoTracking().FirstOrDefaultAsync(o => o.Id == offerId);
        }

        public async Task<string?> GetOfferDataAsync(int offerId)
        {
            if (_isOnline)
            {
                try
                {
                    using var pgContext = new AppDbContext(_pgOptions);
                    pgContext.Database.SetCommandTimeout(10);
                    var data = await pgContext.Offers.AsNoTracking()
                        .Where(o => o.Id == offerId).Select(o => o.Data).FirstOrDefaultAsync();
                    if (!string.IsNullOrEmpty(data)) return data;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[DB] Ошибка PG при загрузке данных: {ex.Message}. Переход в офлайн.");
                    _isOnline = false;
                }
            }

            try
            {
                using var localCtx = new ManagerContext(_connections[0]);
                localCtx.Database.SetCommandTimeout(10);
                var data = await localCtx.Offers.AsNoTracking()
                    .Where(o => o.Id == offerId).Select(o => o.Data).FirstOrDefaultAsync();
                if (!string.IsNullOrEmpty(data)) return data;
            }
            catch (Exception ex) { Trace.WriteLine($"[DB] Ошибка SQLite: {ex.Message}"); }

            return null;
        }

        public async Task<Offer> SaveOfferAsync(
            string? orderNumber, string? companyName, float amount, float material, float services,
            bool isAgent, string? autor, string? actPath, string? dataJson, int managerId)
        {
            if (managerId == 0)
            {
                using var tempCtx = new ManagerContext(_connections[0]);
                var currentUser = await tempCtx.Managers.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.MachineName == Environment.MachineName);
                if (currentUser == null)
                    currentUser = await tempCtx.Managers.AsNoTracking()
                        .FirstOrDefaultAsync(m => m.Contact == Environment.MachineName);
                if (currentUser != null) managerId = currentUser.Id;
            }

            bool isDraftManager = await IsDraftManagerAsync();

            // Для расчетного менеджера — используем его локальный Id
            if (isDraftManager)
            {
                using var draftCtx = new ManagerContext(_connections[0]);
                var draftManager = await draftCtx.Managers.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Name == "Расчетный менеджер");
                if (draftManager != null) managerId = draftManager.Id;
            }

            var offer = new Offer
            {
                N = orderNumber,
                Company = companyName,
                Amount = amount,
                Material = material,
                Services = services,
                Agent = isAgent,
                Autor = autor,
                Act = actPath,
                Data = dataJson,
                CreatedDate = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc),
                ManagerId = managerId
            };

            // ОНЛАЙН + НЕ расчетный менеджер → сохраняем в PG
            if (_isOnline && !isDraftManager)
            {
                try
                {
                    using var pgContext = new AppDbContext(_pgOptions);
                    pgContext.Database.SetCommandTimeout(30);

                    using var localCtxForName = new ManagerContext(_connections[0]);
                    var localManager = await localCtxForName.Managers.AsNoTracking()
                        .FirstOrDefaultAsync(m => m.Id == managerId);

                    int pgManagerId = managerId;
                    if (localManager != null)
                    {
                        var pgManager = await pgContext.Managers.AsNoTracking()
                            .FirstOrDefaultAsync(m => m.Name == localManager.Name);
                        if (pgManager != null) pgManagerId = pgManager.Id;
                    }

                    var pgOffer = new Offer
                    {
                        N = offer.N,
                        Company = offer.Company,
                        Amount = offer.Amount,
                        Material = offer.Material,
                        Services = offer.Services,
                        Agent = offer.Agent,
                        Invoice = offer.Invoice,
                        CreatedDate = DateTime.SpecifyKind(offer.CreatedDate!.Value, DateTimeKind.Utc),
                        Order = offer.Order,
                        Autor = offer.Autor,
                        Act = offer.Act,
                        ManagerId = pgManagerId,
                        Data = offer.Data
                    };

                    pgContext.Offers.Add(pgOffer);
                    await pgContext.SaveChangesAsync();

                    offer.Id = pgOffer.Id;
                    offer.IsPendingSync = false;
                    Trace.WriteLine($"✅ Расчёт сохранён в PG с Id={pgOffer.Id}");
                    return offer;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[DB] Ошибка PG при сохранении: {ex.Message}. Переход в оффлайн.");
                    _isOnline = false;
                }
            }

            // ОФФЛАЙН или РАСЧЕТНЫЙ менеджер → сохраняем ТОЛЬКО локально
            using var offlineCtx = new ManagerContext(_connections[0]);
            offlineCtx.Database.SetCommandTimeout(10);
            var offlineOffer = new Offer
            {
                N = offer.N,
                Company = offer.Company,
                Amount = offer.Amount,
                Material = offer.Material,
                Services = offer.Services,
                Agent = offer.Agent,
                Invoice = offer.Invoice,
                CreatedDate = offer.CreatedDate,
                Order = offer.Order,
                Autor = offer.Autor,
                Act = offer.Act,
                ManagerId = managerId,
                Data = offer.Data,
                IsPendingSync = !isDraftManager
            };

            offlineCtx.Offers.Add(offlineOffer);
            await offlineCtx.SaveChangesAsync();

            // Явно устанавливаем IsPendingSync=False для расчетного менеджера
            // (обход HasDefaultValue(true) в конфигурации контекста)
            if (isDraftManager)
            {
                offlineOffer.IsPendingSync = false;
                offlineCtx.Offers.Update(offlineOffer);
                await offlineCtx.SaveChangesAsync();
            }

            offer.Id = offlineOffer.Id;
            offer.IsPendingSync = offlineOffer.IsPendingSync;

            Trace.WriteLine($"💾 Расчёт сохранён локально с Id={offlineOffer.Id}");
            return offer;
        }

        public async Task<bool> UpdateOfferAsync(Offer updatedOffer)
        {
            if (updatedOffer == null) return false;

            // Расчетный расчёт — обновляем ТОЛЬКО локально
            if (await IsDraftOfferAsync(updatedOffer.Id))
            {
                try
                {
                    using var localCtx = new ManagerContext(_connections[0]);
                    localCtx.Database.SetCommandTimeout(10);
                    var localOffer = await localCtx.Offers.FirstOrDefaultAsync(o => o.Id == updatedOffer.Id);
                    if (localOffer != null)
                    {
                        localOffer.Agent = updatedOffer.Agent;
                        localOffer.Invoice = updatedOffer.Invoice;
                        localOffer.Order = updatedOffer.Order;
                        localOffer.Act = updatedOffer.Act;
                        localOffer.EndDate = updatedOffer.EndDate;
                        await localCtx.SaveChangesAsync();
                        Trace.WriteLine($"✅ Расчётный расчёт Id={updatedOffer.Id} обновлён локально");
                        return true;
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[DB] Ошибка SQLite: {ex.Message}");
                    return false;
                }
            }

            // Обычный расчёт онлайн → обновляем в PG
            if (_isOnline)
            {
                try
                {
                    using var pgContext = new AppDbContext(_pgOptions);
                    pgContext.Database.SetCommandTimeout(10);
                    var pgOffer = await pgContext.Offers.FirstOrDefaultAsync(o => o.Id == updatedOffer.Id);
                    if (pgOffer != null)
                    {
                        pgOffer.Agent = updatedOffer.Agent;
                        pgOffer.Invoice = updatedOffer.Invoice;
                        pgOffer.Order = updatedOffer.Order;
                        pgOffer.Act = updatedOffer.Act;
                        pgOffer.EndDate = updatedOffer.EndDate;
                        await pgContext.SaveChangesAsync();
                        Trace.WriteLine($"✅ Расчёт Id={updatedOffer.Id} обновлён в PG");
                        return true;
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[DB] Ошибка PG: {ex.Message}. Переход в офлайн.");
                    _isOnline = false;
                }
            }

            // Оффлайн: обновляем локально и помечаем для синхронизации
            try
            {
                using var localCtx = new ManagerContext(_connections[0]);
                localCtx.Database.SetCommandTimeout(10);
                var localOffer = await localCtx.Offers.FirstOrDefaultAsync(o => o.Id == updatedOffer.Id);
                if (localOffer != null)
                {
                    localOffer.Agent = updatedOffer.Agent;
                    localOffer.Invoice = updatedOffer.Invoice;
                    localOffer.Order = updatedOffer.Order;
                    localOffer.Act = updatedOffer.Act;
                    localOffer.EndDate = updatedOffer.EndDate;
                    localOffer.IsPendingSync = true;
                    await localCtx.SaveChangesAsync();
                    Trace.WriteLine($"✅ Расчёт Id={updatedOffer.Id} обновлён локально");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DB] Ошибка SQLite: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RemoveOfferAsync(int offerId)
        {
            // Расчетный расчёт — удаляем ТОЛЬКО из локалки
            if (await IsDraftOfferAsync(offerId))
            {
                try
                {
                    using var localCtx = new ManagerContext(_connections[0]);
                    localCtx.Database.SetCommandTimeout(10);
                    var localOffer = await localCtx.Offers.FirstOrDefaultAsync(o => o.Id == offerId);
                    if (localOffer != null)
                    {
                        localCtx.Offers.Remove(localOffer);
                        await localCtx.SaveChangesAsync();
                        Trace.WriteLine($"✅ Расчетный расчёт Id={offerId} удалён из локальной SQLite");
                        return true;
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[DB] Ошибка SQLite: {ex.Message}");
                    return false;
                }
            }

            // Обычный расчёт — удаляем из PG и локалки
            bool pgRemoved = false;
            bool localRemoved = false;

            if (_isOnline)
            {
                try
                {
                    using var pgContext = new AppDbContext(_pgOptions);
                    pgContext.Database.SetCommandTimeout(10);
                    var pgOffer = await pgContext.Offers.FirstOrDefaultAsync(o => o.Id == offerId);
                    if (pgOffer != null)
                    {
                        pgContext.Offers.Remove(pgOffer);
                        await pgContext.SaveChangesAsync();
                        pgRemoved = true;
                        Trace.WriteLine($"✅ Расчёт Id={offerId} удалён из PG");
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[DB] Ошибка PG: {ex.Message}. Переход в офлайн.");
                    _isOnline = false;
                }
            }

            try
            {
                using var localCtx = new ManagerContext(_connections[0]);
                localCtx.Database.SetCommandTimeout(10);
                var localOffer = await localCtx.Offers.FirstOrDefaultAsync(o => o.Id == offerId);
                if (localOffer != null)
                {
                    localCtx.Offers.Remove(localOffer);
                    await localCtx.SaveChangesAsync();
                    localRemoved = true;
                    Trace.WriteLine($"✅ Расчёт Id={offerId} удалён из локальной SQLite");
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DB] Ошибка SQLite: {ex.Message}");
            }

            return pgRemoved || localRemoved;
        }

        /// <summary>
        /// Ищет расчёты по подстроке. При онлайне ищет в PG И в локальной SQLite,
        /// объединяя результаты без фильтрации дубликатов.
        /// При оффлайне ищет только в локальной SQLite.
        /// </summary>
        public async Task<List<Offer>> SearchOffersAsync(int localManagerId, string managerName, string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<Offer>();
            string normalizedQuery = query.ToLower().Replace(" ", "");

            if (_isOnline)
            {
                try
                {
                    using var pgContext = new AppDbContext(_pgOptions);
                    pgContext.Database.SetCommandTimeout(10);
                    var pgManager = await pgContext.Managers.AsNoTracking()
                        .FirstOrDefaultAsync(m => m.Name == managerName);

                    if (pgManager != null)
                    {
                        using var localCtxForId = new ManagerContext(_connections[0]);
                        var correctLocalManager = await localCtxForId.Managers.AsNoTracking()
                            .FirstOrDefaultAsync(m => m.Name == managerName);
                        int finalLocalManagerId = correctLocalManager?.Id ?? localManagerId;

                        // ⭐ 1. Поиск в PG
                        var pgOffers = await pgContext.Offers.AsNoTracking()
                            .Where(o => o.ManagerId == pgManager.Id &&
                                ((o.N != null && o.N.ToLower().Replace(" ", "").Contains(normalizedQuery)) ||
                                 (o.Company != null && o.Company.ToLower().Replace(" ", "").Contains(normalizedQuery)) ||
                                 (o.Invoice != null && o.Invoice.ToLower().Replace(" ", "").Contains(normalizedQuery)) ||
                                 (o.Order != null && o.Order.ToLower().Replace(" ", "").Contains(normalizedQuery))))
                            .OrderByDescending(o => o.Id)
                            .Select(o => new Offer
                            {
                                Id = o.Id,
                                N = o.N,
                                Company = o.Company,
                                Amount = o.Amount,
                                Material = o.Material,
                                Services = o.Services,
                                Agent = o.Agent,
                                Invoice = o.Invoice,
                                CreatedDate = o.CreatedDate,
                                EndDate = o.EndDate,
                                Order = o.Order,
                                Autor = o.Autor,
                                Act = o.Act,
                                ManagerId = finalLocalManagerId,
                                IsPendingSync = false,
                                IsLocalOffer = false
                            }).ToListAsync();

                        // ⭐ 2. Поиск в локальной SQLite
                        var localOffers = await localCtxForId.Offers.AsNoTracking()
                            .Where(o => o.ManagerId == finalLocalManagerId &&
                                ((o.N != null && o.N.ToLower().Replace(" ", "").Contains(normalizedQuery)) ||
                                 (o.Company != null && o.Company.ToLower().Replace(" ", "").Contains(normalizedQuery)) ||
                                 (o.Invoice != null && o.Invoice.ToLower().Replace(" ", "").Contains(normalizedQuery)) ||
                                 (o.Order != null && o.Order.ToLower().Replace(" ", "").Contains(normalizedQuery))))
                            .OrderByDescending(o => o.Id)
                            .Select(o => new Offer
                            {
                                Id = o.Id,
                                N = o.N,
                                Company = o.Company,
                                Amount = o.Amount,
                                Material = o.Material,
                                Services = o.Services,
                                Agent = o.Agent,
                                Invoice = o.Invoice,
                                CreatedDate = o.CreatedDate,
                                EndDate = o.EndDate,
                                Order = o.Order,
                                Autor = o.Autor,
                                Act = o.Act,
                                ManagerId = o.ManagerId,
                                IsPendingSync = o.IsPendingSync,
                                IsLocalOffer = true
                            }).ToListAsync();

                        // ⭐ 3. Просто объединяем: PG + локальные (без фильтрации)
                        var result = pgOffers.Concat(localOffers)
                            .OrderByDescending(o => o.Id)
                            .ToList();

                        Trace.WriteLine($"🔍 Поиск '{query}': PG={pgOffers.Count}, локальных={localOffers.Count}, всего={result.Count}");
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[DB] Ошибка PG при поиске: {ex.Message}. Переход в офлайн.");
                    _isOnline = false;
                }
            }

            // ⭐ Оффлайн: только локальная SQLite
            using var localCtx = new ManagerContext(_connections[0]);
            localCtx.Database.SetCommandTimeout(10);
            var localOffersOffline = await localCtx.Offers.AsNoTracking()
                .Where(o => o.ManagerId == localManagerId &&
                    ((o.N != null && o.N.ToLower().Replace(" ", "").Contains(normalizedQuery)) ||
                     (o.Company != null && o.Company.ToLower().Replace(" ", "").Contains(normalizedQuery)) ||
                     (o.Invoice != null && o.Invoice.ToLower().Replace(" ", "").Contains(normalizedQuery)) ||
                     (o.Order != null && o.Order.ToLower().Replace(" ", "").Contains(normalizedQuery))))
                .OrderByDescending(o => o.Id)
                .Select(o => new Offer
                {
                    Id = o.Id,
                    N = o.N,
                    Company = o.Company,
                    Amount = o.Amount,
                    Material = o.Material,
                    Services = o.Services,
                    Agent = o.Agent,
                    Invoice = o.Invoice,
                    CreatedDate = o.CreatedDate,
                    EndDate = o.EndDate,
                    Order = o.Order,
                    Autor = o.Autor,
                    Act = o.Act,
                    ManagerId = o.ManagerId,
                    IsPendingSync = o.IsPendingSync,
                    IsLocalOffer = true
                }).ToListAsync();

            Trace.WriteLine($"🔍 Поиск '{query}' (оффлайн): найдено {localOffersOffline.Count} расчётов");
            return localOffersOffline;
        }

        public async Task<int> GetNewOffersCountAsync(int localManagerId, string managerName, DateTime since)
        {
            if (_isOnline)
            {
                try
                {
                    using var pgContext = new AppDbContext(_pgOptions);
                    pgContext.Database.SetCommandTimeout(10);
                    var pgManager = await pgContext.Managers.AsNoTracking()
                        .FirstOrDefaultAsync(m => m.Name == managerName);
                    if (pgManager != null)
                    {
                        var allOffers = await pgContext.Offers.AsNoTracking()
                            .Where(o => o.ManagerId == pgManager.Id)
                            .Select(o => new
                            {
                                o.Autor,
                                CreatedDate = o.CreatedDate.HasValue
                                    ? (DateTime?)DateTime.SpecifyKind(o.CreatedDate.Value, DateTimeKind.Utc) : null
                            }).ToListAsync();
                        return allOffers.Count(o =>
                            !string.IsNullOrEmpty(o.Autor) && !o.Autor.Contains(managerName)
                            && o.CreatedDate.HasValue && o.CreatedDate.Value > since);
                    }
                }
                catch { _isOnline = false; }
            }

            using var localCtx = new ManagerContext(_connections[0]);
            localCtx.Database.SetCommandTimeout(10);
            var localOffers = await localCtx.Offers.AsNoTracking()
                .Where(o => o.ManagerId == localManagerId)
                .Select(o => new { o.Autor, o.CreatedDate }).ToListAsync();
            return localOffers.Count(o =>
                !string.IsNullOrEmpty(o.Autor) && !o.Autor.Contains(managerName)
                && o.CreatedDate.HasValue && o.CreatedDate.Value > since);
        }

        public async Task<int?> FindOfferIdByActAsync(string actPath, int localManagerId, string managerName)
        {
            if (string.IsNullOrEmpty(actPath)) return null;

            if (_isOnline)
            {
                try
                {
                    using var pgContext = new AppDbContext(_pgOptions);
                    pgContext.Database.SetCommandTimeout(10);
                    var pgManager = await pgContext.Managers.AsNoTracking()
                        .FirstOrDefaultAsync(m => m.Name == managerName);
                    if (pgManager != null)
                    {
                        var offer = await pgContext.Offers.AsNoTracking()
                            .FirstOrDefaultAsync(o => o.ManagerId == pgManager.Id && o.Act == actPath);
                        return offer?.Id;
                    }
                }
                catch { _isOnline = false; }
            }

            using var localCtx = new ManagerContext(_connections[0]);
            localCtx.Database.SetCommandTimeout(10);
            var localOffer = await localCtx.Offers.AsNoTracking()
                .FirstOrDefaultAsync(o => o.ManagerId == localManagerId && o.Act == actPath);
            return localOffer?.Id;
        }

        public async Task<List<Offer>> GetOffersInProductionAsync(int localManagerId, string managerName)
        {
            if (_isOnline)
            {
                try
                {
                    using var pgContext = new AppDbContext(_pgOptions);
                    pgContext.Database.SetCommandTimeout(10);
                    var pgManager = await pgContext.Managers.AsNoTracking()
                        .FirstOrDefaultAsync(m => m.Name == managerName);
                    if (pgManager != null)
                    {
                        using var localCtxForId = new ManagerContext(_connections[0]);
                        var correctLocalManager = await localCtxForId.Managers.AsNoTracking()
                            .FirstOrDefaultAsync(m => m.Name == managerName);
                        int finalLocalManagerId = correctLocalManager?.Id ?? localManagerId;

                        var offers = await pgContext.Offers.AsNoTracking()
                            .Where(o => o.ManagerId == pgManager.Id && o.EndDate == null
                                && o.Order != null && o.Order != "")
                            .OrderByDescending(o => o.Id)
                            .Select(o => new Offer
                            {
                                Id = o.Id,
                                N = o.N,
                                Company = o.Company,
                                Amount = o.Amount,
                                Material = o.Material,
                                Services = o.Services,
                                Agent = o.Agent,
                                Invoice = o.Invoice,
                                CreatedDate = o.CreatedDate,
                                EndDate = o.EndDate,
                                Order = o.Order,
                                Autor = o.Autor,
                                Act = o.Act,
                                ManagerId = finalLocalManagerId,
                                IsPendingSync = false
                            }).ToListAsync();
                        offers.Reverse();
                        return offers;
                    }
                }
                catch { _isOnline = false; }
            }

            using var localCtx = new ManagerContext(_connections[0]);
            localCtx.Database.SetCommandTimeout(10);
            var localOffers = await localCtx.Offers.AsNoTracking()
                .Where(o => o.ManagerId == localManagerId && o.EndDate == null
                    && o.Order != null && o.Order != "")
                .OrderByDescending(o => o.Id)
                .Select(o => new Offer
                {
                    Id = o.Id,
                    N = o.N,
                    Company = o.Company,
                    Amount = o.Amount,
                    Material = o.Material,
                    Services = o.Services,
                    Agent = o.Agent,
                    Invoice = o.Invoice,
                    CreatedDate = o.CreatedDate,
                    EndDate = o.EndDate,
                    Order = o.Order,
                    Autor = o.Autor,
                    Act = o.Act,
                    ManagerId = o.ManagerId,
                    IsPendingSync = false
                }).ToListAsync();
            localOffers.Reverse();
            return localOffers;
        }

        public async Task<List<Offer>> GetShippedOffersAsync(int localManagerId, string managerName, int month, int year)
        {
            DateTime start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime end = start.AddMonths(1);

            if (_isOnline)
            {
                try
                {
                    using var pgContext = new AppDbContext(_pgOptions);
                    pgContext.Database.SetCommandTimeout(10);
                    var pgManager = await pgContext.Managers.AsNoTracking()
                        .FirstOrDefaultAsync(m => m.Name == managerName);
                    if (pgManager != null)
                    {
                        using var localCtxForId = new ManagerContext(_connections[0]);
                        var correctLocalManager = await localCtxForId.Managers.AsNoTracking()
                            .FirstOrDefaultAsync(m => m.Name == managerName);
                        int finalLocalManagerId = correctLocalManager?.Id ?? localManagerId;

                        var offers = await pgContext.Offers.AsNoTracking()
                            .Where(o => o.ManagerId == pgManager.Id && o.EndDate.HasValue
                                && o.EndDate.Value >= start && o.EndDate.Value < end)
                            .OrderByDescending(o => o.Id)
                            .Select(o => new Offer
                            {
                                Id = o.Id,
                                N = o.N,
                                Company = o.Company,
                                Amount = o.Amount,
                                Material = o.Material,
                                Services = o.Services,
                                Agent = o.Agent,
                                Invoice = o.Invoice,
                                CreatedDate = o.CreatedDate,
                                EndDate = o.EndDate,
                                Order = o.Order,
                                Autor = o.Autor,
                                Act = o.Act,
                                ManagerId = finalLocalManagerId,
                                Data = o.Data,
                                IsPendingSync = false
                            }).ToListAsync();
                        offers.Reverse();
                        return offers;
                    }
                }
                catch { _isOnline = false; }
            }

            using var localCtx = new ManagerContext(_connections[0]);
            localCtx.Database.SetCommandTimeout(10);
            var localOffers = await localCtx.Offers.AsNoTracking()
                .Where(o => o.ManagerId == localManagerId && o.EndDate.HasValue
                    && o.EndDate.Value >= start && o.EndDate.Value < end)
                .OrderByDescending(o => o.Id)
                .Select(o => new Offer
                {
                    Id = o.Id,
                    N = o.N,
                    Company = o.Company,
                    Amount = o.Amount,
                    Material = o.Material,
                    Services = o.Services,
                    Agent = o.Agent,
                    Invoice = o.Invoice,
                    CreatedDate = o.CreatedDate,
                    EndDate = o.EndDate,
                    Order = o.Order,
                    Autor = o.Autor,
                    Act = o.Act,
                    ManagerId = o.ManagerId,
                    Data = o.Data,
                    IsPendingSync = false
                }).ToListAsync();
            localOffers.Reverse();
            return localOffers;
        }

        public async Task<int> GetTotalOffersCountAsync(int localManagerId, string managerName)
        {
            if (_isOnline)
            {
                try
                {
                    using var pgContext = new AppDbContext(_pgOptions);
                    var pgManager = await pgContext.Managers.AsNoTracking()
                        .FirstOrDefaultAsync(m => m.Name == managerName);
                    if (pgManager != null)
                        return await pgContext.Offers.AsNoTracking().CountAsync(o => o.ManagerId == pgManager.Id);
                }
                catch { _isOnline = false; }
            }

            using var localCtx = new ManagerContext(_connections[0]);
            return await localCtx.Offers.AsNoTracking().CountAsync(o => o.ManagerId == localManagerId);
        }

        public async Task<List<Offer>> GetSalesReportAsync(DateTime from, DateTime to, bool isAdmin, string? managerName)
        {
            if (!_isOnline)
            {
                Trace.WriteLine("⚠️ Отчет по продажам недоступен: нет соединения с сервером");
                return new List<Offer>();
            }
            try
            {
                using var pgContext = new AppDbContext(_pgOptions);
                pgContext.Database.SetCommandTimeout(30);
                DateTime fromUtc = DateTime.SpecifyKind(from, DateTimeKind.Utc);
                DateTime toUtc = DateTime.SpecifyKind(to.AddDays(1), DateTimeKind.Utc);

                IQueryable<Offer> query = pgContext.Offers.AsNoTracking()
                    .Include(o => o.Manager)
                    .Where(o => !string.IsNullOrWhiteSpace(o.Order)
                        && o.EndDate.HasValue && o.EndDate.Value >= fromUtc && o.EndDate.Value < toUtc);

                if (!isAdmin && !string.IsNullOrWhiteSpace(managerName))
                    query = query.Where(o => o.Manager != null && o.Manager.Name == managerName);

                var offers = await query.OrderByDescending(o => o.EndDate).ToListAsync();
                Trace.WriteLine($"📊 Отчет по продажам: загружено {offers.Count} расчётов");
                return offers;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"❌ Ошибка загрузки отчета по продажам: {ex.Message}");
                return new List<Offer>();
            }
        }

        public async Task<List<Offer>> GetProductionReportAsync(DateTime from, DateTime to, bool isAdmin, string? managerName)
        {
            if (!_isOnline)
            {
                Trace.WriteLine("⚠️ Отчет по производству недоступен");
                return new List<Offer>();
            }
            try
            {
                using var pgContext = new AppDbContext(_pgOptions);
                pgContext.Database.SetCommandTimeout(30);

                IQueryable<Offer> query = pgContext.Offers.AsNoTracking()
                    .Include(o => o.Manager)
                    .Where(o => !string.IsNullOrWhiteSpace(o.Order) && !o.EndDate.HasValue);

                bool filterByDate = from != DateTime.MinValue && to != DateTime.MaxValue;
                if (filterByDate)
                {
                    DateTime fromUtc = DateTime.SpecifyKind(from, DateTimeKind.Utc);
                    DateTime toUtc = DateTime.SpecifyKind(to.AddDays(1), DateTimeKind.Utc);
                    query = query.Where(o => o.CreatedDate.HasValue
                        && o.CreatedDate.Value >= fromUtc && o.CreatedDate.Value < toUtc);
                }

                if (!isAdmin && !string.IsNullOrWhiteSpace(managerName))
                    query = query.Where(o => o.Manager != null && o.Manager.Name == managerName);

                var offers = await query.OrderByDescending(o => o.CreatedDate).ToListAsync();
                Trace.WriteLine($"🏭 Отчет по производству: загружено {offers.Count} расчётов");
                return offers;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"❌ Ошибка отчета по производству: {ex.Message}");
                return new List<Offer>();
            }
        }

        /// <summary>
        /// Проверяет, является ли менеджер "расчетным" (предназначен для предварительных расчётов).
        /// Расчётный менеджер определяется по имени "Расчетный менеджер" и отсутствию MachineName/Contact.
        /// </summary>
        public async Task<bool> IsDraftManagerAsync()
        {
            using var localCtx = new ManagerContext(_connections[0]);
            var draftManager = await localCtx.Managers.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Name == "Расчетный менеджер");

            if (draftManager == null) return false;

            return string.IsNullOrWhiteSpace(draftManager.MachineName)
                && string.IsNullOrWhiteSpace(draftManager.Contact);
        }

        /// <summary>
        /// Проверяет, является ли расчёт "расчетным" (принадлежит расчетному менеджеру).
        /// </summary>
        public async Task<bool> IsDraftOfferAsync(int offerId)
        {
            using var localCtx = new ManagerContext(_connections[0]);
            var offer = await localCtx.Offers.AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == offerId);
            if (offer == null) return false;

            var manager = await localCtx.Managers.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == offer.ManagerId);
            if (manager == null) return false;

            return manager.Name == "Расчетный менеджер"
                && string.IsNullOrWhiteSpace(manager.MachineName)
                && string.IsNullOrWhiteSpace(manager.Contact);
        }

        //----------------Заказчики----------------//
        #region

        /// <summary>
        /// Добавляет заказчика. При онлайне — ТОЛЬКО в PG, при оффлайне — в локальную SQLite.
        /// </summary>
        public async Task<Customer?> AddCustomerAsync(string name, string? address, bool isAgent, int deliveryPrice, int localManagerId, bool isEngineer)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            string normalizedName = NormalizeCustomerName(name);
            string defaultSpecTemplateJson = JsonSerializer.Serialize(new SpecTemplate());

            using var localCtxForManager = new ManagerContext(_connections[0]);
            var localManager = await localCtxForManager.Managers.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == localManagerId);
            if (localManager == null) return null;

            // Проверка локального дубликата
            var localExistingCustomers = await localCtxForManager.Customers.AsNoTracking()
                .Where(c => c.ManagerId == localManagerId).Select(c => new { c.Id, c.Name }).ToListAsync();
            if (localExistingCustomers.Any(c => NormalizeCustomerName(c.Name) == normalizedName))
            {
                Trace.WriteLine($"ℹ️ Заказчик '{name}' уже существует локально");
                return null;
            }

            if (_isOnline)
            {
                try
                {
                    using var pgContext = new AppDbContext(_pgOptions);
                    pgContext.Database.SetCommandTimeout(10);

                    // Глобальная проверка в PG
                    var allPgCustomers = await pgContext.Customers.AsNoTracking()
                        .Select(c => new { c.Id, c.Name, c.ManagerId }).ToListAsync();
                    var globalDuplicate = allPgCustomers
                        .FirstOrDefault(c => NormalizeCustomerName(c.Name) == normalizedName);
                    if (globalDuplicate != null)
                    {
                        Trace.WriteLine($"⛔ ГЛОБАЛЬНЫЙ ДУБЛИКАТ: '{name}' уже существует в PG (ManagerId={globalDuplicate.ManagerId})");
                        return null;
                    }

                    if (!isEngineer)
                    {
                        var pgManager = await pgContext.Managers.AsNoTracking()
                            .FirstOrDefaultAsync(m => m.Name == localManager.Name);
                        if (pgManager != null)
                        {
                            var pgCustomer = new Customer
                            {
                                Name = name.Trim(),
                                Address = address,
                                Agent = isAgent,
                                DeliveryPrice = deliveryPrice,
                                ManagerId = pgManager.Id,
                                SpecTemplateJson = defaultSpecTemplateJson
                            };
                            pgContext.Customers.Add(pgCustomer);
                            await pgContext.SaveChangesAsync();
                            Trace.WriteLine($"✅ Заказчик '{name}' добавлен в PG с Id={pgCustomer.Id}");
                            return pgCustomer;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[DB] Ошибка PG: {ex.Message}. Переход в офлайн.");
                    _isOnline = false;
                }
            }

            // Оффлайн (или инженер): сохраняем только в локальную SQLite
            using var localCtx = new ManagerContext(_connections[0]);
            localCtx.Database.SetCommandTimeout(10);
            var localCustomer = new Customer
            {
                Name = name.Trim(),
                Address = address,
                Agent = isAgent,
                DeliveryPrice = deliveryPrice,
                ManagerId = localManagerId
            };
            localCtx.Customers.Add(localCustomer);
            await localCtx.SaveChangesAsync();
            Trace.WriteLine($"✅ Заказчик '{name}' добавлен в локальную SQLite с Id={localCustomer.Id}");
            return localCustomer;
        }

        /// <summary>
        /// Обновляет заказчика. При онлайне — ТОЛЬКО в PG, при оффлайне — в локальную SQLite.
        /// </summary>
        public async Task<bool> UpdateCustomerAsync(Customer updatedCustomer, bool isOwner)
        {
            if (updatedCustomer == null) return false;

            if (_isOnline && isOwner)
            {
                try
                {
                    using var pgContext = new AppDbContext(_pgOptions);
                    pgContext.Database.SetCommandTimeout(10);
                    using var localCtxForManager = new ManagerContext(_connections[0]);
                    var localManager = await localCtxForManager.Managers.AsNoTracking()
                        .FirstOrDefaultAsync(m => m.Id == updatedCustomer.ManagerId);
                    if (localManager != null)
                    {
                        var pgManager = await pgContext.Managers.AsNoTracking()
                            .FirstOrDefaultAsync(m => m.Name == localManager.Name);
                        if (pgManager != null)
                        {
                            var pgCustomer = await pgContext.Customers
                                .FirstOrDefaultAsync(c => c.ManagerId == pgManager.Id && c.Name == updatedCustomer.Name);
                            if (pgCustomer != null)
                            {
                                pgCustomer.Address = updatedCustomer.Address;
                                pgCustomer.Agent = updatedCustomer.Agent;
                                pgCustomer.DeliveryPrice = updatedCustomer.DeliveryPrice;
                                await pgContext.SaveChangesAsync();
                                Trace.WriteLine($"✅ Заказчик '{updatedCustomer.Name}' обновлён в PG");
                                return true;
                            }
                        }
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[DB] Ошибка PG: {ex.Message}. Переход в офлайн.");
                    _isOnline = false;
                }
            }

            // Оффлайн (или не владелец): обновляем только локальную SQLite
            try
            {
                using var localCtx = new ManagerContext(_connections[0]);
                localCtx.Database.SetCommandTimeout(10);
                var localManager = await localCtx.Managers.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == updatedCustomer.ManagerId);
                if (localManager != null)
                {
                    var localCustomer = await localCtx.Customers
                        .FirstOrDefaultAsync(c => c.ManagerId == localManager.Id && c.Name == updatedCustomer.Name);
                    if (localCustomer != null)
                    {
                        localCustomer.Address = updatedCustomer.Address;
                        localCustomer.Agent = updatedCustomer.Agent;
                        localCustomer.DeliveryPrice = updatedCustomer.DeliveryPrice;
                        await localCtx.SaveChangesAsync();
                        Trace.WriteLine($"✅ Заказчик '{updatedCustomer.Name}' обновлён локально");
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DB] Ошибка SQLite: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Удаляет заказчика. При онлайне — ТОЛЬКО из PG, при оффлайне — из локальной SQLite.
        /// </summary>
        public async Task<bool> RemoveCustomerAsync(Customer customerToDelete, bool isOwner)
        {
            if (customerToDelete == null) return false;

            if (_isOnline && isOwner)
            {
                try
                {
                    using var pgContext = new AppDbContext(_pgOptions);
                    pgContext.Database.SetCommandTimeout(10);
                    using var localCtxForManager = new ManagerContext(_connections[0]);
                    var localManager = await localCtxForManager.Managers.AsNoTracking()
                        .FirstOrDefaultAsync(m => m.Id == customerToDelete.ManagerId);
                    if (localManager != null)
                    {
                        var pgManager = await pgContext.Managers.AsNoTracking()
                            .FirstOrDefaultAsync(m => m.Name == localManager.Name);
                        if (pgManager != null)
                        {
                            var pgCustomer = await pgContext.Customers
                                .FirstOrDefaultAsync(c => c.ManagerId == pgManager.Id && c.Name == customerToDelete.Name);
                            if (pgCustomer != null)
                            {
                                pgContext.Customers.Remove(pgCustomer);
                                await pgContext.SaveChangesAsync();
                                Trace.WriteLine($"✅ Заказчик '{customerToDelete.Name}' удалён из PG");
                                return true;
                            }
                        }
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[DB] Ошибка PG: {ex.Message}. Переход в офлайн.");
                    _isOnline = false;
                }
            }

            // Оффлайн (или не владелец): удаляем только из локальной SQLite
            try
            {
                using var localCtx = new ManagerContext(_connections[0]);
                localCtx.Database.SetCommandTimeout(10);
                var localManager = await localCtx.Managers.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == customerToDelete.ManagerId);
                if (localManager != null)
                {
                    var localCustomer = await localCtx.Customers
                        .FirstOrDefaultAsync(c => c.ManagerId == localManager.Id && c.Name == customerToDelete.Name);
                    if (localCustomer != null)
                    {
                        int rowsAffected = await localCtx.Database.ExecuteSqlInterpolatedAsync(
                            $"DELETE FROM Customers WHERE Id = {localCustomer.Id}");
                        if (rowsAffected > 0)
                        {
                            Trace.WriteLine($"✅ Заказчик '{customerToDelete.Name}' удалён из локальной SQLite");
                            return true;
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DB] Ошибка SQLite: {ex.Message}");
                return false;
            }
        }

        public static string NormalizeCustomerName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";
            string normalized = name.ToLowerInvariant();
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[-_.]", "");
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", "");
            return normalized;
        }

        /// <summary>
        /// Синхронизирует заказчиков из PG в локальную SQLite для указанного пользователя.
        /// Полностью заменяет локальных заказчиков этого менеджера на PG-шных.
        /// ⭐ Вызывается при загрузке данных ЛЮБОГО менеджера (не только инженера),
        /// чтобы локальная копия была актуальной для оффлайн-работы.
        /// </summary>
        public async System.Threading.Tasks.Task SyncCustomersToLocalAsync(string managerName)
        {
            if (!_isOnline) return;
            try
            {
                using var pgContext = new AppDbContext(_pgOptions);
                var pgManager = await pgContext.Managers.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Name == managerName);
                if (pgManager == null) return;

                using var localCtx = new ManagerContext(_connections[0]);
                var localManager = await localCtx.Managers.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Name == managerName);
                if (localManager == null) return;

                var pgCustomers = await pgContext.Customers.AsNoTracking()
                    .Where(c => c.ManagerId == pgManager.Id).ToListAsync();

                var existingLocal = await localCtx.Customers
                    .Where(c => c.ManagerId == localManager.Id).ToListAsync();
                localCtx.Customers.RemoveRange(existingLocal);

                foreach (var pgCust in pgCustomers)
                {
                    var newLocalCust = new Customer
                    {
                        Name = pgCust.Name,
                        Address = pgCust.Address,
                        Agent = pgCust.Agent,
                        DeliveryPrice = pgCust.DeliveryPrice,
                        ManagerId = localManager.Id
                    };
                    if (!string.IsNullOrEmpty(pgCust.SpecTemplateJson))
                    {
                        try
                        {
                            var template = JsonSerializer.Deserialize<SpecTemplate>(pgCust.SpecTemplateJson);
                            if (template != null) newLocalCust.SpecTemplate = template;
                        }
                        catch { }
                    }
                    localCtx.Customers.Add(newLocalCust);
                }

                await localCtx.SaveChangesAsync();
                Trace.WriteLine($"🔄 Синхронизированы заказчики для '{managerName}' ({pgCustomers.Count} шт.)");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Ошибка синхронизации заказчиков: {ex.Message}");
            }
        }

        public async Task<Customer?> FindCustomerByNameAsync(string normalizedName)
        {
            if (string.IsNullOrWhiteSpace(normalizedName)) return null;

            using var localCtx = new ManagerContext(_connections[0]);
            var localCustomers = await localCtx.Customers.AsNoTracking().ToListAsync();
            var localMatch = localCustomers.FirstOrDefault(c => NormalizeCustomerName(c.Name) == normalizedName);
            if (localMatch != null)
            {
                return new Customer
                {
                    Name = localMatch.Name,
                    Address = localMatch.Address,
                    Agent = localMatch.Agent,
                    DeliveryPrice = localMatch.DeliveryPrice,
                    SpecTemplate = localMatch.SpecTemplate
                };
            }

            if (_isOnline)
            {
                try
                {
                    using var pgContext = new AppDbContext(_pgOptions);
                    var pgCustomers = await pgContext.Customers.AsNoTracking().ToListAsync();
                    var pgMatch = pgCustomers.FirstOrDefault(c => NormalizeCustomerName(c.Name) == normalizedName);
                    if (pgMatch != null)
                    {
                        return new Customer
                        {
                            Name = pgMatch.Name,
                            Address = pgMatch.Address,
                            Agent = pgMatch.Agent,
                            DeliveryPrice = pgMatch.DeliveryPrice,
                            SpecTemplate = !string.IsNullOrEmpty(pgMatch.SpecTemplateJson)
                                ? JsonSerializer.Deserialize<SpecTemplate>(pgMatch.SpecTemplateJson) : new SpecTemplate()
                        };
                    }
                }
                catch { }
            }
            return null;
        }

        public async Task<(string? OwnerName, string? ActualCustomerName)> FindCustomerOwnerInPgAsync(string customerName)
        {
            if (!_isOnline || string.IsNullOrWhiteSpace(customerName)) return (null, null);
            try
            {
                using var pgContext = new AppDbContext(_pgOptions);
                pgContext.Database.SetCommandTimeout(10);

                var exactMatch = await pgContext.Customers.AsNoTracking()
                    .Where(c => c.Name == customerName)
                    .Select(c => new { c.Name, ManagerName = c.Manager != null ? c.Manager.Name : null })
                    .FirstOrDefaultAsync();
                if (exactMatch != null) return (exactMatch.ManagerName, exactMatch.Name);

                string normalizedInput = NormalizeCustomerName(customerName);
                var allCustomers = await pgContext.Customers.AsNoTracking()
                    .Where(c => c.Name != null)
                    .Select(c => new { c.Name, ManagerName = c.Manager != null ? c.Manager.Name : null })
                    .ToListAsync();

                var normalizedMatch = allCustomers.FirstOrDefault(c => NormalizeCustomerName(c.Name) == normalizedInput);
                if (normalizedMatch != null) return (normalizedMatch.ManagerName, normalizedMatch.Name);

                var substringMatch = allCustomers
                    .FirstOrDefault(c => c.Name != null && c.Name.Contains(customerName, StringComparison.OrdinalIgnoreCase));
                if (substringMatch != null) return (substringMatch.ManagerName, substringMatch.Name);

                return (null, null);
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"⚠️ Ошибка поиска заказчика в PG: {ex.Message}");
                return (null, null);
            }
        }

        public async Task<bool> CustomerExistsAsync(string managerName, string normalizedName)
        {
            if (string.IsNullOrWhiteSpace(normalizedName)) return false;

            if (_isOnline)
            {
                try
                {
                    using var pgContext = new AppDbContext(_pgOptions);
                    var pgManager = await pgContext.Managers.AsNoTracking()
                        .FirstOrDefaultAsync(m => m.Name == managerName);
                    if (pgManager != null)
                    {
                        var customers = await pgContext.Customers.AsNoTracking()
                            .Where(c => c.ManagerId == pgManager.Id).Select(c => c.Name).ToListAsync();
                        if (customers.Any(c => NormalizeCustomerName(c) == normalizedName)) return true;
                    }
                }
                catch { }
            }

            using var localCtx = new ManagerContext(_connections[0]);
            var localManager = await localCtx.Managers.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Name == managerName);
            if (localManager != null)
            {
                var localCustomers = await localCtx.Customers.AsNoTracking()
                    .Where(c => c.ManagerId == localManager.Id).Select(c => c.Name).ToListAsync();
                return localCustomers.Any(c => NormalizeCustomerName(c) == normalizedName);
            }
            return false;
        }
        #endregion
    }
}