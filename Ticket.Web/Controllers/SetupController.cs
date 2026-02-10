using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Ticket.Application.Abstractions;
using Ticket.Infrastructure.Identity;
using Ticket.Infrastructure.Persistence;
using Ticket.Web.Setup;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;

namespace Ticket.Web.Controllers
{
    [AllowAnonymous]
    [Route("Setup")]
    public class SetupController : Controller
    {
        private readonly IDataProtector _protector;
        private readonly ISetupConfigStore _store;
        private readonly ISetupState _setupState;
        private readonly IServiceProvider _sp;

        public SetupController(
            IDataProtectionProvider dp,
            ISetupConfigStore store,
            ISetupState setupState,
            IServiceProvider sp)
        {
            _protector = dp.CreateProtector("Ticket.Setup.ConnectionString.v1");
            _store = store;
            _setupState = setupState;
            _sp = sp;
        }
        // Helper methods
        private static int ToInt32OrDefault(object? value, int defaultValue = 0)
        {
            if (value is null || value == DBNull.Value) return defaultValue;
            return Convert.ToInt32(value);
        }

        private static string ToStringOr(object? value, string fallback = "UNKNOWN")
        {
            if (value is null || value == DBNull.Value) return fallback;
            return value.ToString() ?? fallback;
        }


        private static string BuildMasterConnectionString(SetupViewModel vm)
        {
            var b = new SqlConnectionStringBuilder(BuildConnectionString(vm))
            {
                InitialCatalog = "master"
            };
            return b.ConnectionString;
        }

        private static string DiagnoseSqlException(SqlException ex)
        {
            // Birden çok error olabilir; ilk numarayı baz alalım:
            var num = ex.Errors.Count > 0 ? ex.Errors[0].Number : ex.Number;

            return num switch
            {
                26 => "SQL Server/Instance bulunamadı. (Server adı veya instance yanlış olabilir. Named instance ise SQL Browser/UDP 1434 gerekebilir.)",
                53 => "Ağ yolu bulunamadı. (Firewall/port 1433 kapalı olabilir, DNS/host adı çözülemiyor olabilir.)",
                18456 => "Login failed. (Windows Auth yetkisi yok veya SQL Auth kullanıcı/şifre yanlış.)",
                -2146893019 => "TLS/sertifika doğrulama hatası olabilir. (Encrypt/TrustServerCertificate ayarlarını kontrol edin.)",
                _ => $"SQL hatası (Number={num}). Detay: {ex.Message}"
            };
        }

        // End Helper methods

        [HttpGet("")]
        public IActionResult Index()
        {
            if (_setupState.IsConfigured)
                return Redirect("/");

            return View(new SetupViewModel());
        }

        [HttpPost("Test")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Test(SetupViewModel vm)
        {
            if (!ModelState.IsValid)
                return View("Index", vm);

            if (vm.UseSqlAuth && (string.IsNullOrWhiteSpace(vm.UserName) || string.IsNullOrWhiteSpace(vm.Password)))
            {
                ModelState.AddModelError("", "SQL Authentication seçiliyse UserName ve Password zorunludur.");
                return View("Index", vm);
            }

            var report = new List<string>();

            report.Add($"📁 Config root: {SetupPaths.RootDir}");
            report.Add($"📄 setup.json: {SetupPaths.SetupJsonPath} {(System.IO.File.Exists(SetupPaths.SetupJsonPath) ? "(var)" : "(yok)")}");
            report.Add($"🔑 DP keys: {SetupPaths.KeysDir} {(Directory.Exists(SetupPaths.KeysDir) ? "(var)" : "(yok)")}");
            report.Add($"🧭 LocalAppData: {Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}");
            report.Add($"🧭 ProgramData: {Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)}");

            var p = Environment.GetEnvironmentVariable("TICKET_CONNECTION", EnvironmentVariableTarget.Process);
            var u = Environment.GetEnvironmentVariable("TICKET_CONNECTION", EnvironmentVariableTarget.User);
            var m = Environment.GetEnvironmentVariable("TICKET_CONNECTION", EnvironmentVariableTarget.Machine);
            report.Add($"🌍 TICKET_CONNECTION var mı? Process={(string.IsNullOrWhiteSpace(p) ? "yok" : "var")}, User={(string.IsNullOrWhiteSpace(u) ? "yok" : "var")}, Machine={(string.IsNullOrWhiteSpace(m) ? "yok" : "var")}");


            var masterCs = BuildMasterConnectionString(vm);
            var targetDb = vm.Database;

            try
            {
                // 1) Master'a bağlan: Server'a erişim + TLS/Encrypt + login kimliği + server rollerini buradan okuruz
                await using var con = new SqlConnection(masterCs);
                await con.OpenAsync();

                report.Add("✅ SQL bağlantısı: OK (master)");

                // 2) Encrypt gerçekten açık mı? (VIEW SERVER STATE istemeyen yol)
                await using (var cmd = new SqlCommand(
                    "SELECT CONNECTIONPROPERTY('encrypt_option') AS encrypt_option;", con))
                {
                    var enc = await cmd.ExecuteScalarAsync();
                    report.Add($"🔒 encrypt_option: {ToStringOr(enc)}");
                }

                await using (var cmd = new SqlCommand(@"
                   SELECT
                       CONNECTIONPROPERTY('encrypt_option') AS encrypt_option,
                       CONNECTIONPROPERTY('auth_scheme')   AS auth_scheme;", con))
                await using (var r = await cmd.ExecuteReaderAsync())
                {
                    if (await r.ReadAsync())
                    {
                        report.Add($"🔒 encrypt_option: {ToStringOr(r.GetValue(0))}");
                        report.Add($"🔑 auth_scheme: {ToStringOr(r.GetValue(1))}");
                    }
                }


                // 3) Server / Version
                await using (var cmd = new SqlCommand("SELECT @@SERVERNAME;", con))
                {
                    var srv = await cmd.ExecuteScalarAsync();
                    report.Add($"🖥️ Server: {ToStringOr(srv)}");
                }

                await using (var cmd = new SqlCommand("SELECT @@VERSION;", con))
                {
                    var v = ToStringOr(await cmd.ExecuteScalarAsync(), "");
                    report.Add($"ℹ️ Version: {(v.Split('\n').FirstOrDefault() ?? v)}");
                }

                // 4) Uygulama hangi kullanıcıyla bağlandı? (çok kritik: IIS/AppPool identity vs)
                await using (var cmd = new SqlCommand(
                    "SELECT SYSTEM_USER, SUSER_SNAME(), ORIGINAL_LOGIN();", con))
                await using (var r = await cmd.ExecuteReaderAsync())
                {
                    if (await r.ReadAsync())
                    {
                        report.Add($"👤 SYSTEM_USER: {ToStringOr(r.GetValue(0))}");
                        report.Add($"👤 SUSER_SNAME: {ToStringOr(r.GetValue(1))}");
                        report.Add($"👤 ORIGINAL_LOGIN: {ToStringOr(r.GetValue(2))}");
                    }
                }

                // 5) Server rolleri: sysadmin/dbcreator
                await using (var cmd = new SqlCommand("SELECT IS_SRVROLEMEMBER('sysadmin'), IS_SRVROLEMEMBER('dbcreator');", con))
                await using (var r = await cmd.ExecuteReaderAsync())
                {
                    if (await r.ReadAsync())
                    {
                        var isSysAdmin = ToInt32OrDefault(r.GetValue(0)) == 1;
                        var isDbCreator = ToInt32OrDefault(r.GetValue(1)) == 1;

                        report.Add(isSysAdmin ? "✅ Server role: sysadmin" : "⚠️ Server role: sysadmin değil");
                        report.Add(isDbCreator ? "✅ Server role: dbcreator" : "⚠️ Server role: dbcreator değil");
                    }
                }

                // 6) DB var mı?
                bool dbExists;
                await using (var cmd = new SqlCommand("SELECT CASE WHEN DB_ID(@db) IS NULL THEN 0 ELSE 1 END;", con))
                {
                    cmd.Parameters.AddWithValue("@db", targetDb);
                    dbExists = ToInt32OrDefault(await cmd.ExecuteScalarAsync()) == 1;
                }
                report.Add(dbExists ? $"✅ DB mevcut: {targetDb}" : $"⚠️ DB yok: {targetDb}");

                // 7) DB yoksa oluşturma yetkisi var mı?
                if (!dbExists)
                {
                    await using var cmd = new SqlCommand("SELECT HAS_PERMS_BY_NAME(NULL, 'SERVER', 'CREATE ANY DATABASE');", con);
                    var scalar = await cmd.ExecuteScalarAsync();

                    if (scalar is null || scalar == DBNull.Value)
                        report.Add("⚠️ CREATE ANY DATABASE kontrolü: BELİRSİZ (NULL döndü). sysadmin/dbcreator satırına bakın.");
                    else
                        report.Add(ToInt32OrDefault(scalar) == 1
                            ? "✅ CREATE ANY DATABASE yetkisi var."
                            : "❌ CREATE ANY DATABASE yetkisi yok.");
                }
                else
                {
                    // 8) DB varsa kritik tablolar var mı? (yarım kurulum / migration uygulanmamış senaryosu)
                    var targetCs = BuildConnectionString(vm);
                    await using var conDb = new SqlConnection(targetCs);
                    await conDb.OpenAsync();

                    async Task<bool> HasObject(string name)
                    {
                        await using var c = new SqlCommand("SELECT OBJECT_ID(@n);", conDb);
                        c.Parameters.AddWithValue("@n", name);
                        var val = await c.ExecuteScalarAsync();
                        return val is not null && val != DBNull.Value;
                    }

                    var hasMigrations = await HasObject("dbo.__EFMigrationsHistory");
                    report.Add(hasMigrations ? "✅ __EFMigrationsHistory var (migration izleniyor)." : "⚠️ __EFMigrationsHistory yok (migration uygulanmamış olabilir).");

                    var hasRoles = await HasObject("dbo.AspNetRoles");
                    var hasUsers = await HasObject("dbo.AspNetUsers");
                    report.Add(hasRoles ? "✅ AspNetRoles var (Identity tabloları var)." : "⚠️ AspNetRoles yok (Identity tabloları kurulmamış).");
                    report.Add(hasUsers ? "✅ AspNetUsers var." : "⚠️ AspNetUsers yok.");

                    // 9) Migration history varsa son migration'ı göster (varsa)
                    if (hasMigrations)
                    {
                        try
                        {
                            await using var c = new SqlCommand("SELECT TOP(1) MigrationId FROM dbo.__EFMigrationsHistory ORDER BY MigrationId DESC;", conDb);
                            var lastMig = await c.ExecuteScalarAsync();
                            report.Add($"🧩 Son migration: {ToStringOr(lastMig, "BULUNAMADI")}");
                        }
                        catch
                        {
                            // History tablosu var ama yetki/şema farkı olabilir, sessiz geç
                        }
                    }
                }

                // Ek: kullanıcıya hatırlatma
                report.Add("💡 Not: IIS’e aldığınızda bağlanan Windows hesabı değişebilir (AppPool identity). Bu ekranda görünen SYSTEM_USER buna göre kontrol edilmelidir.");

                TempData["Ok"] = string.Join(Environment.NewLine, report);
            }
            catch (SqlException ex)
            {
                // SQL hatalarını Number’a göre teşhis et
                TempData["Err"] = "Bağlantı testi başarısız:\n" + DiagnoseSqlException(ex);
            }
            catch (Exception ex)
            {
                TempData["Err"] = "Bağlantı testi başarısız:\n" + ex.Message;
            }

            return View("Index", vm);
        }



        [HttpPost("Install")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Install(SetupViewModel vm)
        {
            if (!ModelState.IsValid)
                return View("Index", vm);

            if (vm.UseSqlAuth && (string.IsNullOrWhiteSpace(vm.UserName) || string.IsNullOrWhiteSpace(vm.Password)))
            {
                ModelState.AddModelError("", "SQL Authentication seçiliyse UserName ve Password zorunludur.");
                return View("Index", vm);
            }

            var cs = BuildConnectionString(vm);

            // 0) DB yoksa önce oluştur (master üzerinden)
            // Not: Migrate, hedef DB'ye bağlanmayı dener. DB yoksa patlar.

            var masterCs = BuildMasterConnectionString(vm);

            await using (var con = new SqlConnection(masterCs))
            {
                await con.OpenAsync();

                await using var check = new SqlCommand("SELECT DB_ID(@db);", con);
                check.Parameters.AddWithValue("@db", vm.Database);

                var exists = await check.ExecuteScalarAsync();
                if (exists is null || exists == DBNull.Value)
                {
                    var dbName = QuoteSqlIdentifier(vm.Database);

                    await using var create = new SqlCommand($"CREATE DATABASE {dbName};", con);
                    await create.ExecuteNonQueryAsync();
                }
            }






            // 1) Önce migrate (kurulum gerçekten tamamlanabilsin)
            var opts = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(cs, x => x.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
                .Options;




            await using (var ctx = new AppDbContext(opts))
            {
                await ctx.Database.MigrateAsync();
            }

            // 2) Migrate başarılıysa ayarı kaydet

            // RememberMe zorunlu (Model A)
            _store.Save(new SetupConfig
            {
                EncryptedConnectionString = _protector.Protect(cs),
                SavedAtUtc = DateTime.UtcNow
            });



            await SeedIdentityManually(cs);
            await SeedPermissions(cs);




            TempData["Ok"] = "Kurulum tamamlandı (Migrate + Seed). Artık giriş yapabilirsiniz.";
            return Redirect("/Account/Login");
        }


        private static string BuildConnectionString(SetupViewModel vm)
        {

            var b = new SqlConnectionStringBuilder
            {
                DataSource = vm.Server,
                InitialCatalog = vm.Database,
                Encrypt = vm.Encrypt,
                TrustServerCertificate = vm.TrustServerCertificate,
                // Integrated Security = Windows Authentication
                IntegratedSecurity = !vm.UseSqlAuth,
            };

            if (vm.UseSqlAuth)
            {
                b.UserID = vm.UserName ?? "";
                b.Password = vm.Password ?? "";
            }

            // Not: EF/ADO için connection pooling default açık gelir; yeterli.
            return b.ConnectionString;
        }


        private static string QuoteSqlIdentifier(string name)
        {
            // SQL identifier içinde ] varsa escape eder
            return $"[{name.Replace("]", "]]")}]";
        }

        private static readonly (string Key, string Name)[] DefaultPermissions =
        {
            ("Tickets.View", "Ticket görüntüle"),
            ("Tickets.Create", "Ticket oluştur"),
            ("Tickets.Edit", "Ticket düzenle"),
            ("Tickets.Assign", "Ticket ata"),
            ("Customers.View", "Müşteri görüntüle"),
            ("Customers.Edit", "Müşteri ekle/düzenle"),
            ("Problems.View", "Sorun/Hizmet görüntüle"),
            ("Problems.Edit", "Sorun/Hizmet ekle/düzenle"),
            ("Reports.View", "Raporları görüntüle"),
            ("Admin.Users", "Kullanıcı yönetimi"),
            ("Admin.Permissions", "Yetki yönetimi"),
        };

        private static async Task SeedPermissions(string cs)
        {
            var opts = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(cs, x => x.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
                .Options;

            await using var db = new AppDbContext(opts);

            foreach (var (key, name) in DefaultPermissions)
            {
                var exists = await db.Permissions.AnyAsync(p => p.Key == key);
                if (!exists)
                    db.Permissions.Add(new Permission { Key = key, Name = name });
            }
            await db.SaveChangesAsync();

            var adminRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
            if (adminRole is not null)
            {
                var existing = await db.RolePermissions
                    .Where(rp => rp.RoleId == adminRole.Id)
                    .Select(rp => rp.PermissionKey)
                    .ToListAsync();

                foreach (var key in DefaultPermissions.Select(x => x.Key).Except(existing))
                    db.RolePermissions.Add(new RolePermission { RoleId = adminRole.Id, PermissionKey = key });

                await db.SaveChangesAsync();
            }

            var userRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "User");
            if (userRole is not null)
            {
                var wanted = new[] { "Tickets.View", "Tickets.Create", "Customers.View", "Problems.View" };

                var existing = await db.RolePermissions
                    .Where(rp => rp.RoleId == userRole.Id)
                    .Select(rp => rp.PermissionKey)
                    .ToListAsync();

                foreach (var key in wanted.Except(existing))
                    db.RolePermissions.Add(new RolePermission { RoleId = userRole.Id, PermissionKey = key });

                await db.SaveChangesAsync();
            }
        }

        private static async Task SeedIdentityManually(string cs)
        {
            var opts = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(cs, x => x.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
                .Options;

            await using var db = new AppDbContext(opts);

            // Rolleri ekle (idempotent)
            async Task<AppRole> EnsureRole(string name)
            {
                var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == name);
                if (role != null) return role;

                role = new AppRole
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    NormalizedName = name.ToUpperInvariant(),
                    ConcurrencyStamp = Guid.NewGuid().ToString()
                };

                db.Roles.Add(role);
                await db.SaveChangesAsync();
                return role;
            }

            var adminRole = await EnsureRole("Admin");
            var userRole = await EnsureRole("User");

            // User ekle (idempotent)
            async Task<AppUser> EnsureUser(string userName, string password)
            {
                var existing = await db.Users.FirstOrDefaultAsync(u => u.UserName == userName);
                if (existing != null) return existing;

                var email = $"{userName.ToLowerInvariant()}@ticket.local";

                var user = new AppUser
                {
                    Id = Guid.NewGuid(),
                    UserName = userName,
                    NormalizedUserName = userName.ToUpperInvariant(),
                    Email = email,
                    NormalizedEmail = email.ToUpperInvariant(),
                    EmailConfirmed = true,
                    SecurityStamp = Guid.NewGuid().ToString(),
                    ConcurrencyStamp = Guid.NewGuid().ToString()
                };

                // Şifre hash
                var hasher = new PasswordHasher<AppUser>();
                user.PasswordHash = hasher.HashPassword(user, password);

                db.Users.Add(user);
                await db.SaveChangesAsync();

                return user;
            }

            async Task EnsureUserInRole(AppUser user, AppRole role)
            {
                var exists = await db.UserRoles.AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id);
                if (exists) return;

                db.UserRoles.Add(new IdentityUserRole<Guid>
                {
                    UserId = user.Id,
                    RoleId = role.Id
                });

                await db.SaveChangesAsync();
            }

            // Kullanıcıları oluştur
            var admin = await EnsureUser("Admin", "1234");
            var u1 = await EnsureUser("User1", "1234");
            var u2 = await EnsureUser("User2", "1234");
            var u3 = await EnsureUser("User3", "1234");

            // Rollerini ata
            await EnsureUserInRole(admin, adminRole);
            await EnsureUserInRole(u1, userRole);
            await EnsureUserInRole(u2, userRole);
            await EnsureUserInRole(u3, userRole);
        }



    }
}
