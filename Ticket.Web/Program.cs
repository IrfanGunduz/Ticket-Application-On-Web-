using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Ticket.Application.Abstractions;
using Ticket.Infrastructure.Identity;
using Ticket.Infrastructure.Persistence;
using Ticket.Web.Auth;
using Ticket.Web.Setup;
using Ticket.Infrastructure.Email;
using Ticket.Web.Email;
using System.IO;


var builder = WebApplication.CreateBuilder(args);

// =========================
// MVC (global auth policy)
// =========================
builder.Services.AddControllersWithViews(options =>
{
    // Varsayýlan: her yerde login zorunlu (Setup/Account controller'larý [AllowAnonymous] ile muaf)
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});

// =========================
// Data Protection (setup config decrypt için kalýcý key)
// =========================
var dataRoot = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "Ticket");

var keysPath = Path.Combine(dataRoot, "keys");
Directory.CreateDirectory(keysPath);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
    .SetApplicationName("Ticket");


// =========================
// Setup services
// =========================
builder.Services.AddSingleton<ISetupConfigStore, FileSetupConfigStore>();
builder.Services.AddSingleton<ISetupState, SetupState>();
builder.Services.AddSingleton<IConnectionStringProvider, DefaultConnectionStringProvider>();

// =========================
builder.Services.Configure<EmailIngestOptions>(builder.Configuration.GetSection("EmailIngest"));

// þimdilik null reader
//builder.Services.AddScoped<IEmailInboxReader, ImapEmailInboxReader>();
builder.Services.AddScoped<ImapEmailInboxReader>();
builder.Services.AddScoped<Pop3EmailInboxReader>();

builder.Services.AddScoped<IEmailInboxReader, EmailInboxReaderRouter>();

// background worker
builder.Services.AddHostedService<EmailIngestHostedService>();


// =========================
// DbContext
// =========================

builder.Services.AddDbContext<AppDbContext>((sp, opts) =>
{
    var cs = sp.GetRequiredService<IConnectionStringProvider>().GetConnectionStringOrNull();

    if (string.IsNullOrWhiteSpace(cs))
    {
        // Güvenli dummy: yanlýþlýkla connect olursa bile gerçek MSSQL instance'ýna gitmesin.
        cs = @"Server=(localdb)\MSSQLLocalDB;Database=__Ticket_NoSetup__;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True";
    }

    opts.UseSqlServer(cs, x => x.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
});


builder.Services.AddScoped<SetupAwareCookieEvents>();

// =========================
// Identity
// =========================
builder.Services
    .AddDefaultIdentity<AppUser>(options =>
    {
        // 1234 seed için gevþek
        options.Password.RequiredLength = 4;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;

        
    })
    .AddRoles<AppRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";

    options.EventsType = typeof(SetupAwareCookieEvents);

});

// =========================
// Permission-based auth (dinamik policy)
// =========================
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();

// Her request'te (cookie doðrulandýktan sonra) role->permission claim ekleme
// Ýçinde _setup.IsConfigured kontrolü var.
builder.Services.AddScoped<IClaimsTransformation, PermissionsClaimsTransformation>();

var app = builder.Build();

// =========================
// Pipeline
// =========================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// 1) Setup gate: Setup tamam deðilse Setup ve static dýþýnda her þeyi Setup'a yönlendir.

app.Use(async (ctx, next) =>
{
    var setup = ctx.RequestServices.GetRequiredService<ISetupState>();

    var path = ctx.Request.Path.Value ?? "";
    var isSetupPath = path.StartsWith("/Setup", StringComparison.OrdinalIgnoreCase);

    var isStatic =
        path.StartsWith("/css", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/js", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/images", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/favicon", StringComparison.OrdinalIgnoreCase);

    // /Account artýk istisna deðil: Setup bitmeden login'e bile gitmesin.
    if (!setup.IsConfigured && !isSetupPath && !isStatic)
    {
        if (!ctx.Response.HasStarted)
            ctx.Response.Redirect("/Setup");
        return;
    }

    await next();
});


// 2) AuthN/AuthZ sadece Setup tamamlandýysa çalýþsýn.
// Böylece setup aþamasýnda Identity DB'ye dokunamaz.
app.UseWhen(
    ctx => ctx.RequestServices.GetRequiredService<ISetupState>().IsConfigured,
    branch =>
    {
        branch.UseAuthentication();
        branch.UseAuthorization();
    });

// Routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
