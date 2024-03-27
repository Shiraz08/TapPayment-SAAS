using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TapPaymentIntegration.Areas.Identity.Data;
using TapPaymentIntegration.Models.Card;
using TapPaymentIntegration.Models.InvoiceDTO;
using TapPaymentIntegration.Models.PaymentDTO;
using TapPaymentIntegration.Models.Subscription;

namespace TapPaymentIntegration.Data;

public class TapPaymentIntegrationContext : IdentityDbContext<ApplicationUser>
{
    public TapPaymentIntegrationContext(DbContextOptions<TapPaymentIntegrationContext> options)
        : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.EnableDetailedErrors();
    }

    public DbSet<Subscriptions> subscriptions { get; set; }
    public DbSet<ChargeResponse>  chargeResponses { get; set; }
    public DbSet<Invoice>  invoices { get; set; }
    public DbSet<RecurringCharge>  recurringCharges { get; set; } 
    public DbSet<ChangeCardInfo> changeCardInfos { get; set; } 
    public DbSet<UserSubscriptions> userSubscriptions { get; set; } 
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);


    }
}
