using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Serialization;
using System;
using TapPaymentIntegration.Areas.Identity.Data;
using TapPaymentIntegration.Data;
using TapPaymentIntegration.Models;
using TapPaymentIntegration.Models.Email;
using TapPaymentIntegration.Models.HangFire;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("TapPaymentIntegrationContextConnection") ?? throw new InvalidOperationException("Connection string 'TapPaymentIntegrationContextConnection' not found.");
builder.Services.AddDbContext<TapPaymentIntegrationContext>(options =>options.UseSqlServer(connectionString));
builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = false).AddRoles<IdentityRole>().AddEntityFrameworkStores<TapPaymentIntegrationContext>();
builder.Services.AddDbContext<TapPaymentIntegrationContext>(options =>
{
    options.UseSqlServer(connectionString);
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
});

// Add other services
builder.Services.AddAntiforgery();
builder.Services.AddControllers().AddNewtonsoftJson();
builder.Services.AddDataProtection()
                .SetApplicationName("BillingTamarran")
                .PersistKeysToFileSystem(new System.IO.DirectoryInfo(@"/var/38be6598-5608-4b68-9e5e-bc825c61c522/"));
builder.Services.AddControllersWithViews();
builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();
// Configure Ajax settings
builder.Services.AddControllers().AddNewtonsoftJson(options =>
{
    options.SerializerSettings.ContractResolver = new DefaultContractResolver();
});
// Add Hangfire services
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("TapPaymentIntegrationContextConnection"), new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.Zero,
        UseRecommendedIsolationLevel = true,
        DisableGlobalLocks = true
    }));

// Add the processing server as IHostedService
builder.Services.AddHangfireServer();
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

// Session
builder.Services.AddMvc();
builder.Services.AddSession(options => {
    options.IdleTimeout = TimeSpan.FromHours(60);
});
//email sender override class
builder.Services.Configure<DataProtectionTokenProviderOptions>(opt =>
   opt.TokenLifespan = TimeSpan.FromHours(2));
builder.Services.AddTransient<IEmailSender, EmailSender>();
// LOgot user by admin 
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
});
// Ajax Setting
builder.Services.AddControllers().AddNewtonsoftJson(options =>
{
    options.SerializerSettings.ContractResolver = new DefaultContractResolver();
});
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
  //var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
  //var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
  //await StaticRoles.SeedRolesAsync(userManager, roleManager);
  //await StaticRoles.SeedSuperAdminAsync(userManager, roleManager);

}
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseDeveloperExceptionPage();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseRouting();
app.UseAuthorization();
app.UseSession();
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    endpoints.MapRazorPages();
    endpoints.MapControllers();
    endpoints.MapHangfireDashboard();
   RecurringJob.AddOrUpdate<DailyRecurreningJob>(x => x.AutoChargeJob(), Cron.Daily);
   RecurringJob.AddOrUpdate<DailyRecurreningJob>(x => x.AutoChargeJobForBenefit(), Cron.Daily);
   //RecurringJob.AddOrUpdate<DailyRecurreningJob>(x => x.AutoChargeJob(), Cron.MinuteInterval(1));
});
app.Run();
