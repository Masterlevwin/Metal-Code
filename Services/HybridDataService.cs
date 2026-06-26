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
    /// При наличии сети работает с PG, при её отсутствии — с локальной SQLite.
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
            // ⭐ ПРОВЕРКА 1: Отключение PG через конфигурацию
            if (IsPostgresDisabledByConfig())
            {
                _isOnline = false;
                Trace.WriteLine("🔌 PostgreSQL отключён через конфигурацию (DisablePostgres=true)");
                return false;
            }

            // ⭐ ПРОВЕРКА 2: Реальное подключение к PG
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

        /// <summary>
        /// Проверяет, отключён ли PostgreSQL через настройку DisablePostgres в App.config.
        /// </summary>
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

        /// <summary>
        /// Обновляет локальные справочники (Metals, Works, TypeDetails) из PG.
        /// </summary>
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

        /// <summary>
        /// Синхронизирует MachineName текущего пользователя с PG.
        /// НЕ создаёт новых пользователей в PG — список контролируется администратором.
        /// Обновляет список менеджеров локально для инженеров/админов.
        /// </summary>
        public async System.Threading.Tasks.Task SyncManagersAsync(bool isEngineerOrAdmin)
        {
            if (!_isOnline) return;

            try
            {
                using var pgContext = new AppDbContext(_pgOptions);
                using var localCtx = new ManagerContext(_connections[0]);

                // 1. Находим текущего пользователя локально
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

                // 2. ВСЕГДА синхронизируем роли всех менеджеров из PG в локальную SQLite
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

        /// <summary>
        /// Миграция данных из локальной SQLite в PG при первом запуске.
        /// Мигрирует только те данные, которых ещё нет в PG (сравнение по количеству).
        /// </summary>
        public async System.Threading.Tasks.Task MigrateUserDataToPgAsync()
        {
            if (!_isOnline) return;

            try
            {
                using var pgContext = new AppDbContext(_pgOptions);
                using var localCtx = new ManagerContext(_connections[0]);

                // 1. Исправление счётчиков PostgreSQL
                await pgContext.Database.ExecuteSqlRawAsync(@"
            SELECT setval('managers_id_seq', COALESCE((SELECT MAX(id) FROM managers), 1));
            SELECT setval('offers_id_seq', COALESCE((SELECT MAX(id) FROM offers), 1));
            SELECT setval('customers_id_seq', COALESCE((SELECT MAX(id) FROM customers), 1));
        ");

                // 2. Находим текущего пользователя локально
                var localCurrentUser = await localCtx.Managers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.MachineName == Environment.MachineName || m.Contact == Environment.MachineName);

                if (localCurrentUser == null) return;

                // 3. Ищем пользователя в PG по имени
                var pgCurrentUser = await pgContext.Managers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Name == localCurrentUser.Name);

                if (pgCurrentUser == null) return;

                // 4. Обновляем MachineName в PG (если пустой)
                if (string.IsNullOrEmpty(pgCurrentUser.MachineName) && !string.IsNullOrEmpty(localCurrentUser.MachineName))
                {
                    var trackedUser = await pgContext.Managers.FirstOrDefaultAsync(m => m.Id == pgCurrentUser.Id);
                    if (trackedUser != null)
                    {
                        trackedUser.MachineName = localCurrentUser.MachineName;
                        await pgContext.SaveChangesAsync();
                    }
                }

                // 5. Определяем, чьи данные мигрировать
                List<string> managersToMigrate = new();

                if (localCurrentUser.IsEngineer || localCurrentUser.IsAdmin)
                {
                    List<Manager> pgManagers = await pgContext.Managers.AsNoTracking().ToListAsync();
                    managersToMigrate = pgManagers
                        .Where(m => m.Name != null)
                        .Select(m => m.Name!)
                        .ToList();
                }
                else if (localCurrentUser.Name != null)
                {
                    managersToMigrate = new List<string> { localCurrentUser.Name };
                }

                // 6. Мигрируем данные для каждого менеджера
                foreach (var managerName in managersToMigrate)
                {
                    var pgManager = await pgContext.Managers.AsNoTracking().FirstOrDefaultAsync(m => m.Name == managerName);
                    if (pgManager == null) continue;

                    var localIds = await localCtx.Managers
                        .AsNoTracking()
                        .Where(m => m.Name == managerName)
                        .Select(m => m.Id)
                        .ToListAsync();

                    if (!localIds.Any()) continue;

                    Trace.WriteLine($"🔄 Начало миграции данных для '{managerName}'...");

                    // ================================================================
                    // ⭐ ПРОВЕРКА ЗАКАЗЧИКОВ: мигрируем, если в PG меньше, чем локально
                    // ================================================================
                    var localCustomersCount = await localCtx.Customers
                        .AsNoTracking()
                        .Where(c => localIds.Contains(c.ManagerId))
                        .CountAsync();

                    var pgCustomersCount = await pgContext.Customers
                        .AsNoTracking()
                        .Where(c => c.ManagerId == pgManager.Id)
                        .CountAsync();

                    Trace.WriteLine($"📊 Заказчики для '{managerName}': локально={localCustomersCount}, в PG={pgCustomersCount}");

                    if (pgCustomersCount < localCustomersCount)
                    {
                        var localCustomers = await localCtx.Customers
                            .AsNoTracking()
                            .Where(c => localIds.Contains(c.ManagerId))
                            .ToListAsync();

                        // ⭐ Фильтруем только тех, которых нет в PG (по имени)
                        var pgCustomerNames = await pgContext.Customers
                            .AsNoTracking()
                            .Where(c => c.ManagerId == pgManager.Id)
                            .Select(c => c.Name)
                            .ToListAsync();

                        var customersToMigrate = localCustomers
                            .Where(c => !pgCustomerNames.Contains(c.Name))
                            .ToList();

                        if (customersToMigrate.Any())
                        {
                            Trace.WriteLine($"📦 Миграция {customersToMigrate.Count} заказчиков...");

                            foreach (var c in customersToMigrate)
                            {
                                // ⭐ Сериализуем SpecTemplate (owned entity из SQLite) в JSON для PG
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
                        else
                        {
                            Trace.WriteLine($"ℹ️ Все заказчики уже мигрированы для '{managerName}' (разница в количестве из-за удалённых)");
                        }
                    }
                    else
                    {
                        Trace.WriteLine($"ℹ️ Заказчики уже мигрированы для '{managerName}'");
                    }

                    // ================================================================
                    // ⭐ ПРОВЕРКА РАСЧЁТОВ: мигрируем, если в PG меньше, чем локально
                    // ================================================================
                    var localOffersCount = await localCtx.Offers
                        .AsNoTracking()
                        .Where(o => localIds.Contains(o.ManagerId))
                        .CountAsync();

                    var pgOffersCount = await pgContext.Offers
                        .AsNoTracking()
                        .Where(o => o.ManagerId == pgManager.Id)
                        .CountAsync();

                    Trace.WriteLine($"📊 Расчёты для '{managerName}': локально={localOffersCount}, в PG={pgOffersCount}");

                    if (pgOffersCount < localOffersCount)
                    {
                        var localOffers = await localCtx.Offers
                            .AsNoTracking()
                            .Where(o => localIds.Contains(o.ManagerId))
                            .ToListAsync();

                        // ⭐ Фильтруем только те, которых нет в PG (по номеру N)
                        // Номер + ManagerId — естественный ключ расчёта
                        var pgOfferKeys = await pgContext.Offers
                            .AsNoTracking()
                            .Where(o => o.ManagerId == pgManager.Id)
                            .Select(o => o.N)
                            .ToListAsync();

                        var offersToMigrate = localOffers
                            .Where(o => !pgOfferKeys.Contains(o.N))
                            .ToList();

                        if (offersToMigrate.Any())
                        {
                            Trace.WriteLine($"📦 Миграция {offersToMigrate.Count} расчётов пакетами по 10...");

                            const int batchSize = 10;
                            int savedCount = 0;
                            int failedCount = 0;

                            for (int i = 0; i < offersToMigrate.Count; i += batchSize)
                            {
                                var batch = offersToMigrate.Skip(i).Take(batchSize).ToList();

                                foreach (var o in batch)
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
                                                ? DateTime.SpecifyKind(o.EndDate.Value, DateTimeKind.Utc)
                                                : null,
                                            Order = o.Order,
                                            Autor = o.Autor,
                                            Act = o.Act,
                                            ManagerId = pgManager.Id,
                                            Data = o.Data
                                        };

                                        pgContext.Offers.Add(pgOffer);
                                    }
                                    catch (Exception ex)
                                    {
                                        failedCount++;
                                        Trace.WriteLine($"⚠️ Пропуск расчёта {o.N}: {ex.Message}");
                                    }
                                }

                                pgContext.Database.SetCommandTimeout(300);
                                await pgContext.SaveChangesAsync();

                                foreach (var entry in pgContext.ChangeTracker.Entries().ToList())
                                {
                                    entry.State = EntityState.Detached;
                                }

                                savedCount += batch.Count;
                                Trace.WriteLine($"  📦 Пакет {i / batchSize + 1}: сохранено {savedCount} из {offersToMigrate.Count}");
                            }

                            Trace.WriteLine($"✅ Мигрировано {savedCount} расчётов для '{managerName}'" +
                                            (failedCount > 0 ? $" (пропущено: {failedCount})" : ""));
                        }
                        else
                        {
                            Trace.WriteLine($"ℹ️ Все расчёты уже мигрированы для '{managerName}' (разница в количестве из-за удалённых)");
                        }
                    }
                    else
                    {
                        Trace.WriteLine($"ℹ️ Расчёты уже мигрированы для '{managerName}'");
                    }

                    // Помечаем локальные расчёты как синхронизированные
                    await localCtx.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE Offers 
                SET IsPendingSync = 0 
                WHERE ManagerId IN (SELECT Id FROM Managers WHERE Name = {managerName})");

                    Trace.WriteLine($"✅ Завершена миграция для '{managerName}'");
                }

                // 7. Синхронизация ролей из PG в локальную базу
                var pgManagersList = await pgContext.Managers.AsNoTracking().ToListAsync();
                foreach (var pgMgr in pgManagersList)
                {
                    await localCtx.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE Managers 
                SET IsAdmin = {pgMgr.IsAdmin}, 
                    IsEngineer = {pgMgr.IsEngineer}, 
                    IsLaser = {pgMgr.IsLaser}
                WHERE Name = {pgMgr.Name}");
                }
            }
            catch (Exception ex)
            {
                var currentEx = ex;
                int level = 0;
                Trace.WriteLine($"❌ Ошибка миграции на уровне {level}: {currentEx.Message}");

                while (currentEx.InnerException != null)
                {
                    level++;
                    currentEx = currentEx.InnerException;
                    Trace.WriteLine($"  └─ Inner {level}: {currentEx.Message}");
                    if (level > 5) break;
                }

                if (ex is Npgsql.PostgresException pgEx)
                {
                    Trace.WriteLine($"🔴 PostgreSQL Error Code: {pgEx.SqlState}");
                    Trace.WriteLine($"🔴 Constraint Name: {pgEx.ConstraintName}");
                    Trace.WriteLine($"🔴 Table Name: {pgEx.TableName}");
                }

                Trace.WriteLine($"❌ Ошибка миграции (игнорируется, переход в офлайн): {ex.Message}");
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
                                SpecTemplateJson = c.SpecTemplateJson // ⭐ Загружаем JSON из PG
                            })
                            .ToListAsync();

                        return customers;
                    }
                }
                catch
                {
                    _isOnline = false;
                }
            }

            using var localCtx = new ManagerContext(_connections[0]);
            localCtx.Database.SetCommandTimeout(10);

            // ⭐ Для SQLite загружаем с Include, чтобы получить owned entity SpecTemplate
            var localCustomers = await localCtx.Customers.AsNoTracking()
                .Where(c => c.ManagerId == localManagerId)
                .OrderBy(c => c.Name)
                .ToListAsync();

            // ⭐ Для SQLite SpecTemplate уже загружен как owned entity, но нам нужно сериализовать его в JSON
            // чтобы свойство-геттер SpecTemplate работал корректно при передаче между слоями
            foreach (var c in localCustomers)
            {
                // SpecTemplate уже загружен через OwnsOne, просто убеждаемся, что он не null
                if (c.SpecTemplate == null)
                {
                    c.SpecTemplate = new SpecTemplate();
                }
            }

            return localCustomers;
        }

        public async Task<List<Offer>> GetOffersAsync(int localManagerId, string managerName, int count = 50)
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

                        // ⭐ Загружаем 200 самых СВЕЖИХ расчётов
                        var offers = await pgContext.Offers.AsNoTracking()
                            .Where(o => o.ManagerId == pgManager.Id)
                            .OrderByDescending(o => o.Id)
                            .Take(count)
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
                            })
                            .ToListAsync();

                        // ⭐ ПЕРЕВОРАЧИВАЕМ: теперь старые в начале списка, новые — в конце
                        offers.Reverse();
                        return offers;
                    }
                }
                catch
                {
                    _isOnline = false;
                }
            }

            using var localCtx = new ManagerContext(_connections[0]);
            localCtx.Database.SetCommandTimeout(10);

            var localOffers = await localCtx.Offers.AsNoTracking()
                .Where(o => o.ManagerId == localManagerId)
                .OrderByDescending(o => o.Id)
                .Take(count)
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
                })
                .ToListAsync();

            // ⭐ ПЕРЕВОРАЧИВАЕМ и локальную выборку
            localOffers.Reverse();
            return localOffers;
        }

        /// <summary>
        /// Ищет расчёты по подстроке во всей базе (PG или локальной SQLite).
        /// Поиск ведётся по полям: N, Company, Invoice, Order.
        /// Игнорирует регистр и пробелы.
        /// </summary>
        public async Task<List<Offer>> SearchOffersAsync(int localManagerId, string managerName, string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<Offer>();

            // ⭐ Нормализуем запрос: нижний регистр + удаление пробелов
            string normalizedQuery = query.ToLower().Replace(" ", "");

            var result = new List<Offer>();

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

                        // ⭐ Поиск во всей таблице PG с инлайновой нормализацией
                        var pgOffers = await pgContext.Offers.AsNoTracking()
                            .Where(o => o.ManagerId == pgManager.Id &&
                                (
                                    (o.N != null && o.N.ToLower().Replace(" ", "").Contains(normalizedQuery)) ||
                                    (o.Company != null && o.Company.ToLower().Replace(" ", "").Contains(normalizedQuery)) ||
                                    (o.Invoice != null && o.Invoice.ToLower().Replace(" ", "").Contains(normalizedQuery)) ||
                                    (o.Order != null && o.Order.ToLower().Replace(" ", "").Contains(normalizedQuery))
                                ))
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
                            })
                            .ToListAsync();

                        result.AddRange(pgOffers);
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[DB] Ошибка PG при поиске: {ex.Message}. Переход в офлайн.");
                    _isOnline = false;
                }
            }

            // Фоллбек на локальную SQLite
            using var localCtx = new ManagerContext(_connections[0]);
            localCtx.Database.SetCommandTimeout(10);

            var localOffers = await localCtx.Offers.AsNoTracking()
                .Where(o => o.ManagerId == localManagerId &&
                    (
                        (o.N != null && o.N.ToLower().Replace(" ", "").Contains(normalizedQuery)) ||
                        (o.Company != null && o.Company.ToLower().Replace(" ", "").Contains(normalizedQuery)) ||
                        (o.Invoice != null && o.Invoice.ToLower().Replace(" ", "").Contains(normalizedQuery)) ||
                        (o.Order != null && o.Order.ToLower().Replace(" ", "").Contains(normalizedQuery))
                    ))
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
                })
                .ToListAsync();

            result.AddRange(localOffers);
            return result;
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
                catch
                {
                    _isOnline = false;
                }
            }

            using var localCtx = new ManagerContext(_connections[0]);
            localCtx.Database.SetCommandTimeout(30);
            return await localCtx.Offers.AsNoTracking().FirstOrDefaultAsync(o => o.Id == offerId);
        }

        /// <summary>
        /// Загружает только поле Data (JSON с данными расчета) по Id.
        /// Сначала ищет в PG, затем в локальной SQLite.
        /// </summary>
        public async Task<string?> GetOfferDataAsync(int offerId)
        {
            // 1. Сначала ищем в PG (если онлайн)
            if (_isOnline)
            {
                try
                {
                    using var pgContext = new AppDbContext(_pgOptions);
                    pgContext.Database.SetCommandTimeout(10);

                    var data = await pgContext.Offers.AsNoTracking()
                        .Where(o => o.Id == offerId)
                        .Select(o => o.Data)
                        .FirstOrDefaultAsync();

                    if (!string.IsNullOrEmpty(data))
                    {
                        Trace.WriteLine($"📂 Данные расчета Id={offerId} загружены из PG (размер: {data.Length} байт)");
                        return data;
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[DB] Ошибка PG при загрузке данных: {ex.Message}. Переход в офлайн.");
                    _isOnline = false;
                }
            }

            // 2. Фоллбек на локальную SQLite
            try
            {
                using var localCtx = new ManagerContext(_connections[0]);
                localCtx.Database.SetCommandTimeout(10);

                var data = await localCtx.Offers.AsNoTracking()
                    .Where(o => o.Id == offerId)
                    .Select(o => o.Data)
                    .FirstOrDefaultAsync();

                if (!string.IsNullOrEmpty(data))
                {
                    Trace.WriteLine($"📂 Данные расчета Id={offerId} загружены из локальной SQLite (размер: {data.Length} байт)");
                    return data;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DB] Ошибка SQLite при загрузке данных: {ex.Message}");
            }

            Trace.WriteLine($"⚠️ Данные расчета Id={offerId} не найдены ни в PG, ни в SQLite");
            return null;
        }

        /// <summary>
        /// Удаляет расчет из обеих баз (PG и SQLite), чтобы не оставалось "призраков".
        /// </summary>
        public async Task<bool> RemoveOfferAsync(int offerId)
        {
            bool isRemoved = false;

            // 1. Удаляем из PG (если онлайн)
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
                        isRemoved = true;
                        Trace.WriteLine($"✅ Расчет Id={offerId} удален из PG");
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[DB] Ошибка PG при удалении: {ex.Message}. Переход в офлайн.");
                    _isOnline = false;
                }
            }

            // 2. ВСЕГДА удаляем из локальной SQLite
            using var localCtx = new ManagerContext(_connections[0]);
            localCtx.Database.SetCommandTimeout(10);

            var localOffer = await localCtx.Offers.FirstOrDefaultAsync(o => o.Id == offerId);
            if (localOffer != null)
            {
                localCtx.Offers.Remove(localOffer);
                await localCtx.SaveChangesAsync();
                isRemoved = true;
                Trace.WriteLine($"✅ Расчет Id={offerId} удален из локальной SQLite");
            }

            return isRemoved;
        }

        /// <summary>
        /// Сохраняет расчет в базу данных.
        /// Если есть связь с PG — сохраняет туда и делает копию в локальной SQLite.
        /// Если связи нет — сохраняет в локальную SQLite с пометкой IsPendingSync = true.
        /// </summary>
        public async Task<Offer> SaveOfferAsync(
            string? orderNumber, string? companyName, float amount, float material, float services,
            bool isAgent, string? autor, string? actPath, string? dataJson, int managerId)
        {
            if (managerId == 0)
            {
                using var tempCtx = new ManagerContext(_connections[0]);
                var currentUser = await tempCtx.Managers.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.MachineName == Environment.MachineName || m.Contact == Environment.MachineName);
                if (currentUser != null) managerId = currentUser.Id;
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

            Trace.WriteLine($"💾 Новый расчёт: N={offer.N}, CreatedDate={offer.CreatedDate:yyyy-MM-dd HH:mm:ss.fff} UTC, Kind={offer.CreatedDate?.Kind}");

            if (_isOnline)
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

                    Trace.WriteLine($"✅ Расчет сохранен в PG с Id={pgOffer.Id}");

                    offer.Id = pgOffer.Id;
                    offer.IsPendingSync = false;

                    // Сохраняем локальную копию
                    using var localCtx = new ManagerContext(_connections[0]);
                    var localOffer = new Offer
                    {
                        Id = pgOffer.Id,
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
                        IsPendingSync = false
                    };
                    localCtx.Offers.Add(localOffer);
                    await localCtx.SaveChangesAsync();

                    return offer;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[DB] Ошибка PG при сохранении: {ex.Message}. Переход в офлайн.");
                    _isOnline = false;
                }
            }

            // ОФЛАЙН: сохраняем в локальную SQLite с пометкой IsPendingSync
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
                IsPendingSync = true
            };

            offlineCtx.Offers.Add(offlineOffer);
            await offlineCtx.SaveChangesAsync();

            offer.Id = offlineOffer.Id;
            offer.IsPendingSync = true;

            Trace.WriteLine($"💾 Расчет сохранен локально с Id={offlineOffer.Id} (ожидает синхронизации)");
            return offer;
        }

        /// <summary>
        /// Синхронизирует отложенные расчеты (IsPendingSync = true) из локальной SQLite в PG.
        /// </summary>
        public async System.Threading.Tasks.Task SyncPendingOffersAsync()
        {
            if (!_isOnline) return;

            try
            {
                using var localCtx = new ManagerContext(_connections[0]);

                var pendingOffers = await localCtx.Offers
                    .Where(o => o.IsPendingSync)
                    .ToListAsync();

                if (!pendingOffers.Any())
                {
                    Trace.WriteLine("✅ Отложенных расчетов нет.");
                    return;
                }

                Trace.WriteLine($"🔄 Начало синхронизации {pendingOffers.Count} отложенных расчетов...");

                int successCount = 0;
                int failCount = 0;

                foreach (var localOffer in pendingOffers)
                {
                    try
                    {
                        var localManager = await localCtx.Managers.AsNoTracking()
                            .FirstOrDefaultAsync(m => m.Id == localOffer.ManagerId);

                        if (localManager == null) continue;

                        // ⭐ Создаём НОВЫЙ контекст для каждой операции с PG
                        using var pgContext = new AppDbContext(_pgOptions);
                        pgContext.Database.SetCommandTimeout(30);

                        var pgManager = await pgContext.Managers.AsNoTracking()
                            .FirstOrDefaultAsync(m => m.Name == localManager.Name);

                        if (pgManager == null) continue;

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

                        localOffer.IsPendingSync = false;
                        await localCtx.SaveChangesAsync();

                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        Trace.WriteLine($"⚠️ Пропуск расчета Id={localOffer.Id} (ошибка: {ex.Message})");
                    }
                }

                Trace.WriteLine($"✅ Синхронизация завершена: {successCount} успешно, {failCount} пропущено.");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"❌ Критическая ошибка синхронизации: {ex.Message}");
            }
        }

        /// <summary>
        /// Возвращает количество новых расчётов для менеджера, созданных после указанной даты.
        /// </summary>
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
                        // ⭐ Получаем все расчёты и фильтруем в памяти — это надёжнее, чем LINQ-to-SQL
                        // с разными часовыми поясами
                        var allOffers = await pgContext.Offers.AsNoTracking()
                            .Where(o => o.ManagerId == pgManager.Id)
                            .Select(o => new
                            {
                                o.Autor,
                                CreatedDate = o.CreatedDate.HasValue
                                    ? (DateTime?)DateTime.SpecifyKind(o.CreatedDate.Value, DateTimeKind.Utc)
                                    : null
                            })
                            .ToListAsync();

                        return allOffers.Count(o =>
                            !string.IsNullOrEmpty(o.Autor)
                            && !o.Autor.Contains(managerName)
                            && o.CreatedDate.HasValue
                            && o.CreatedDate.Value > since);
                    }
                }
                catch
                {
                    _isOnline = false;
                }
            }

            using var localCtx = new ManagerContext(_connections[0]);
            localCtx.Database.SetCommandTimeout(10);

            var localOffers = await localCtx.Offers.AsNoTracking()
                .Where(o => o.ManagerId == localManagerId)
                .Select(o => new { o.Autor, o.CreatedDate })
                .ToListAsync();

            return localOffers.Count(o =>
                !string.IsNullOrEmpty(o.Autor)
                && !o.Autor.Contains(managerName)
                && o.CreatedDate.HasValue
                && o.CreatedDate.Value > since);
        }

        /// <summary>
        /// Ищет расчёт по пути к файлу (Act) для указанного менеджера.
        /// Возвращает Id расчёта или null, если не найден.
        /// </summary>
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
                catch
                {
                    _isOnline = false;
                }
            }

            using var localCtx = new ManagerContext(_connections[0]);
            localCtx.Database.SetCommandTimeout(10);
            var localOffer = await localCtx.Offers.AsNoTracking()
                .FirstOrDefaultAsync(o => o.ManagerId == localManagerId && o.Act == actPath);
            return localOffer?.Id;
        }

        /// <summary>
        /// Обновляет редактируемые поля расчёта по его уникальному Id.
        /// </summary>
        public async Task<bool> UpdateOfferAsync(Offer updatedOffer)
        {
            if (updatedOffer == null) return false;
            bool success = false;

            // 1. Обновляем в PG (если онлайн)
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
                        success = true;
                        Trace.WriteLine($"✅ Расчёт Id={updatedOffer.Id} обновлён в PG");
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[DB] Ошибка PG при обновлении: {ex.Message}. Переход в офлайн.");
                    _isOnline = false;
                }
            }

            // 2. Обновляем в локальной SQLite (если расчёт там уже есть)
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
                    success = true;
                    Trace.WriteLine($"✅ Расчёт Id={updatedOffer.Id} обновлён в локальной SQLite");
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DB] Ошибка SQLite при обновлении: {ex.Message}");
            }

            return success;
        }

        /// <summary>
        /// Загружает последние N расчётов менеджера (для отображения по умолчанию).
        /// </summary>
        public async Task<List<Offer>> GetRecentOffersAsync(int localManagerId, string managerName, int count = 50)
        {
            return await GetOffersAsync(localManagerId, managerName, count);
        }

        /// <summary>
        /// Загружает расчёты "в производстве" (без EndDate, но с Order).
        /// </summary>
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
                            .Where(o => o.ManagerId == pgManager.Id
                                && o.EndDate == null
                                && o.Order != null
                                && o.Order != "")
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
                            })
                            .ToListAsync();

                        offers.Reverse();
                        return offers;
                    }
                }
                catch { _isOnline = false; }
            }

            using var localCtx = new ManagerContext(_connections[0]);
            localCtx.Database.SetCommandTimeout(10);

            var localOffers = await localCtx.Offers.AsNoTracking()
                .Where(o => o.ManagerId == localManagerId
                    && o.EndDate == null
                    && o.Order != null
                    && o.Order != "")
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
                })
                .ToListAsync();

            localOffers.Reverse();
            return localOffers;
        }

        /// <summary>
        /// Загружает отгруженные расчёты за указанный месяц.
        /// </summary>
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
                            .Where(o => o.ManagerId == pgManager.Id
                                && o.EndDate.HasValue
                                && o.EndDate.Value >= start
                                && o.EndDate.Value < end)
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
                            })
                            .ToListAsync();

                        offers.Reverse();
                        return offers;
                    }
                }
                catch { _isOnline = false; }
            }

            using var localCtx = new ManagerContext(_connections[0]);
            localCtx.Database.SetCommandTimeout(10);

            var localOffers = await localCtx.Offers.AsNoTracking()
                .Where(o => o.ManagerId == localManagerId
                    && o.EndDate.HasValue
                    && o.EndDate.Value >= start
                    && o.EndDate.Value < end)
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
                })
                .ToListAsync();

            localOffers.Reverse();
            return localOffers;
        }

        /// <summary>
        /// Получает общее количество расчётов менеджера (для статистики).
        /// </summary>
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
                    {
                        return await pgContext.Offers.AsNoTracking()
                            .CountAsync(o => o.ManagerId == pgManager.Id);
                    }
                }
                catch { _isOnline = false; }
            }

            using var localCtx = new ManagerContext(_connections[0]);
            return await localCtx.Offers.AsNoTracking()
                .CountAsync(o => o.ManagerId == localManagerId);
        }

        /// <summary>
        /// Загружает данные для отчета по продажам за период из PostgreSQL.
        /// Если isAdmin = false — фильтрует по имени менеджера.
        /// </summary>
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

                // ⭐ Базовый запрос с Include для загрузки навигационного свойства Manager
                IQueryable<Offer> query = pgContext.Offers.AsNoTracking()
                    .Include(o => o.Manager) // ⭐ Загружаем Manager для каждого Offer
                    .Where(o => !string.IsNullOrWhiteSpace(o.Order)
                        && o.EndDate.HasValue
                        && o.EndDate.Value >= fromUtc
                        && o.EndDate.Value < toUtc);

                // ⭐ Фильтр по менеджеру через навигационное свойство
                if (!isAdmin && !string.IsNullOrWhiteSpace(managerName))
                {
                    query = query.Where(o => o.Manager != null && o.Manager.Name == managerName);
                }

                var offers = await query
                    .OrderByDescending(o => o.EndDate)
                    .ToListAsync();

                Trace.WriteLine($"📊 Отчет по продажам: загружено {offers.Count} расчётов за {from:dd.MM.yyyy} — {to:dd.MM.yyyy}");
                return offers;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"❌ Ошибка загрузки отчета по продажам: {ex.Message}");
                return new List<Offer>();
            }
        }

        /// <summary>
        /// Очищает локальную SQLite-базу от устаревших расчётов.
        /// Удаляет расчёты без номера заказа, старше 60 дней, синхронизированные с PG.
        /// Расчёты с номером заказа (в работе) и несинхронизированные расчёты не удаляются.
        /// </summary>
        public async System.Threading.Tasks.Task CleanupLocalOffersAsync()
        {
            try
            {
                using var localCtx = new ManagerContext(_connections[0]);
                localCtx.Database.SetCommandTimeout(30);

                DateTime cutoffDate = DateTime.UtcNow.AddDays(-60);

                var offersToDelete = await localCtx.Offers
                    .Where(o =>
                        o.CreatedDate.HasValue && o.CreatedDate.Value < cutoffDate &&
                        (o.Order == null || o.Order == "") &&
                        !o.IsPendingSync
                    )
                    .ToListAsync();

                if (!offersToDelete.Any())
                {
                    Trace.WriteLine("🧹 Очистка локальной БД: расчётов для удаления не найдено");
                    return;
                }

                int count = offersToDelete.Count;

                localCtx.Offers.RemoveRange(offersToDelete);
                await localCtx.SaveChangesAsync();

                // ⭐ Освобождаем место на диске
                await localCtx.Database.ExecuteSqlRawAsync("VACUUM");

                Trace.WriteLine($"✅ Очистка локальной БД завершена: удалено {count} расчётов");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"❌ Ошибка очистки локальной БД: {ex.Message}");
            }
        }


        /// <summary>
        /// Добавляет нового заказчика.
        /// Проверяет глобальную уникальность имени в PG. Если имя занято другим менеджером, создание блокируется.
        /// </summary>
        public async Task<Customer?> AddCustomerAsync(string name, string? address, bool isAgent, int deliveryPrice, int localManagerId, bool isEngineer)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            string normalizedName = NormalizeCustomerName(name);
            string defaultSpecTemplateJson = System.Text.Json.JsonSerializer.Serialize(new SpecTemplate());

            Customer? pgCustomer = null;
            Customer? localCustomer = null;

            using var localCtxForManager = new ManagerContext(_connections[0]);
            var localManager = await localCtxForManager.Managers.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == localManagerId);
            if (localManager == null) return null;

            // 1. ПРОВЕРКА ЛОКАЛЬНО: есть ли заказчик у текущего менеджера?
            var localExistingCustomers = await localCtxForManager.Customers.AsNoTracking()
                .Where(c => c.ManagerId == localManagerId)
                .Select(c => new { c.Id, c.Name })
                .ToListAsync();

            var localDuplicate = localExistingCustomers
                .FirstOrDefault(c => NormalizeCustomerName(c.Name) == normalizedName);

            if (localDuplicate != null)
            {
                Trace.WriteLine($"ℹ️ Заказчик '{name}' уже существует локально у менеджера '{localManager.Name}'");
                return null;
            }

            // 2. ГЛОБАЛЬНАЯ ПРОВЕРКА В PG: есть ли заказчик с таким именем у ЛЮБОГО менеджера?
            if (_isOnline)
            {
                try
                {
                    using var pgContext = new AppDbContext(_pgOptions);
                    pgContext.Database.SetCommandTimeout(10);

                    // Загружаем всех заказчиков из PG для глобальной проверки
                    var allPgCustomers = await pgContext.Customers.AsNoTracking()
                        .Select(c => new { c.Id, c.Name, c.ManagerId })
                        .ToListAsync();

                    var globalDuplicate = allPgCustomers
                        .FirstOrDefault(c => NormalizeCustomerName(c.Name) == normalizedName);

                    if (globalDuplicate != null)
                    {
                        // ⭐ БЛОКИРУЕМ СОЗДАНИЕ. Не добавляем его локально!
                        Trace.WriteLine($"⛔ ГЛОБАЛЬНЫЙ ДУБЛИКАТ: Заказчик '{name}' уже существует в PG (ManagerId={globalDuplicate.ManagerId}). Создание отменено.");
                        return null;
                    }

                    // 3. Если глобального дубликата нет, создаем в PG (только если это не инженер)
                    if (!isEngineer)
                    {
                        var pgManager = await pgContext.Managers.AsNoTracking()
                            .FirstOrDefaultAsync(m => m.Name == localManager.Name);

                        if (pgManager != null)
                        {
                            pgCustomer = new Customer
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
                        }
                    }
                    else
                    {
                        Trace.WriteLine($"ℹ️ Инженер '{localManager.Name}' создаёт уникального заказчика '{name}' только локально (прошел глобальную проверку).");
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[DB] Ошибка PG при добавлении заказчика: {ex.Message}. Переход в офлайн.");
                    _isOnline = false;
                }
            }

            // 4. ВСЕГДА добавляем в локальную SQLite (если прошли глобальную проверку)
            using var localCtx = new ManagerContext(_connections[0]);
            localCtx.Database.SetCommandTimeout(10);

            localCustomer = new Customer
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
        /// Обновляет данные заказчика.
        /// Если isOwner = false — обновляет только локальную SQLite (не имеет прав на PG).
        /// </summary>
        public async Task<bool> UpdateCustomerAsync(Customer updatedCustomer, bool isOwner)
        {
            if (updatedCustomer == null) return false;
            bool success = false;

            // 1. Обновляем в PG ТОЛЬКО если владелец
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
                                success = true;
                                Trace.WriteLine($"✅ Заказчик '{updatedCustomer.Name}' обновлён в PG");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[DB] Ошибка PG при обновлении заказчика: {ex.Message}. Переход в офлайн.");
                    _isOnline = false;
                }
            }
            else if (!isOwner)
            {
                Trace.WriteLine($"ℹ️ Пользователь не является владельцем '{updatedCustomer.Name}'. Обновляем только локально.");
            }

            // 2. ВСЕГДА обновляем в локальной SQLite
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
                        success = true;
                        Trace.WriteLine($"✅ Заказчик '{updatedCustomer.Name}' обновлён в локальной SQLite");
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DB] Ошибка SQLite при обновлении заказчика: {ex.Message}");
            }

            return success;
        }

        /// <summary>
        /// Удаляет заказчика.
        /// Если isOwner = false — удаляет только из локальной SQLite.
        /// </summary>
        public async Task<bool> RemoveCustomerAsync(Customer customerToDelete, bool isOwner)
        {
            if (customerToDelete == null) return false;

            bool pgRemoved = false;
            bool localRemoved = false;

            Trace.WriteLine($"[DEBUG REMOVE] Начало удаления: Id={customerToDelete.Id}, Name='{customerToDelete.Name}', isOwner={isOwner}");

            // 1. Удаляем из PG ТОЛЬКО если владелец
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
                                pgRemoved = true;
                                Trace.WriteLine($"✅ Заказчик '{customerToDelete.Name}' удалён из PG");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"[DB] Ошибка PG при удалении заказчика: {ex.Message}. Переход в офлайн.");
                    _isOnline = false;
                }
            }
            else if (!isOwner)
            {
                Trace.WriteLine($"ℹ️ Пользователь не является владельцем '{customerToDelete.Name}'. Удаляем только локально.");
            }

            // 2. ВСЕГДА удаляем из локальной SQLite
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
                            localRemoved = true;
                            Trace.WriteLine($"✅ Заказчик '{customerToDelete.Name}' удалён из локальной SQLite");
                        }
                    }
                    else
                    {
                        localRemoved = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[DB] Ошибка SQLite при удалении заказчика: {ex.Message}");
            }

            return pgRemoved || localRemoved;
        }

        /// <summary>
        /// Нормализует имя заказчика для сравнения: приводит к нижнему регистру, удаляет дефисы и пробелы.
        /// "Спец-инжиниринг", "Специнжиниринг", "Спец Инжиниринг" → "специнжиниринг"
        /// </summary>
        public static string NormalizeCustomerName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";

            string normalized = name.ToLowerInvariant();
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[-_.]", "");
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", "");

            return normalized;
        }

        /// <summary>
        /// Синхронизирует заказчиков из PG в локальную SQLite для указанного менеджера.
        /// Используется, когда инженер просматривает расчёты менеджера — 
        /// чтобы у инженера всегда были актуальные данные заказчиков.
        /// Добавляет новых и обновляет существующих, но НЕ удаляет локальных.
        /// </summary>
        /// <summary>
        /// Синхронизирует ВСЕХ заказчиков указанного менеджера из PG в локальную SQLite.
        /// Используется, когда инженер выбирает менеджера для просмотра расчётов.
        /// </summary>
        public async System.Threading.Tasks.Task SyncCustomersForEngineerAsync(string managerName)
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

                // Загружаем ВСЕХ заказчиков из PG
                var pgCustomers = await pgContext.Customers.AsNoTracking()
                    .Where(c => c.ManagerId == pgManager.Id)
                    .ToListAsync();

                // Полная замена локальных заказчиков этого менеджера
                var existingLocal = await localCtx.Customers
                    .Where(c => c.ManagerId == localManager.Id)
                    .ToListAsync();

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
                            if (template != null)
                            {
                                newLocalCust.SpecTemplate = template;
                            }
                        }
                        catch { /* значения по умолчанию */ }
                    }

                    localCtx.Customers.Add(newLocalCust);
                }

                await localCtx.SaveChangesAsync();
                Trace.WriteLine($"🔄 Синхронизированы ВСЕ заказчики для '{managerName}'");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Ошибка синхронизации заказчиков: {ex.Message}");
            }
        }

        /// <summary>
        /// Ищет заказчика по нормализованному имени во всей локальной базе и PG.
        /// Используется для копирования данных (адрес, доставка), если заказчик уже где-то был создан.
        /// </summary>
        public async Task<Customer?> FindCustomerByNameAsync(string normalizedName)
        {
            if (string.IsNullOrWhiteSpace(normalizedName)) return null;

            // 1. Сначала ищем во всей локальной SQLite (у всех менеджеров)
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
                    SpecTemplate = localMatch.SpecTemplate // Копируем шаблон, если он есть
                };
            }

            // 2. Если локально не нашли, ищем в PG (если онлайн)
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
                            // SpecTemplate десериализуем из JSON, если он есть
                            SpecTemplate = !string.IsNullOrEmpty(pgMatch.SpecTemplateJson)
                                ? System.Text.Json.JsonSerializer.Deserialize<SpecTemplate>(pgMatch.SpecTemplateJson)
                                : new SpecTemplate()
                        };
                    }
                }
                catch { /* Игнорируем ошибки сети при фоновом поиске */ }
            }

            return null; // Не найден нигде, будут использованы значения по умолчанию
        }

        /// <summary>
        /// Проверяет, существует ли заказчик с таким нормализованным именем у указанного менеджера в БД.
        /// </summary>
        public async Task<bool> CustomerExistsAsync(string managerName, string normalizedName)
        {
            if (string.IsNullOrWhiteSpace(normalizedName)) return false;

            // 1. Проверяем в PostgreSQL
            if (_isOnline)
            {
                try
                {
                    using var pgContext = new AppDbContext(_pgOptions);
                    var pgManager = await pgContext.Managers.AsNoTracking()
                        .FirstOrDefaultAsync(m => m.Name == managerName);

                    if (pgManager != null)
                    {
                        // Загружаем только имена для экономии памяти
                        var customers = await pgContext.Customers.AsNoTracking()
                            .Where(c => c.ManagerId == pgManager.Id)
                            .Select(c => c.Name)
                            .ToListAsync();

                        if (customers.Any(c => NormalizeCustomerName(c) == normalizedName))
                            return true;
                    }
                }
                catch
                {
                    // Игнорируем ошибки сети при фоновой проверке, перейдем к локальной проверке
                }
            }

            // 2. Проверяем локально в SQLite
            using var localCtx = new ManagerContext(_connections[0]);
            var localManager = await localCtx.Managers.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Name == managerName);

            if (localManager != null)
            {
                var localCustomers = await localCtx.Customers.AsNoTracking()
                    .Where(c => c.ManagerId == localManager.Id)
                    .Select(c => c.Name)
                    .ToListAsync();

                return localCustomers.Any(c => NormalizeCustomerName(c) == normalizedName);
            }

            return false;
        }
    }
}