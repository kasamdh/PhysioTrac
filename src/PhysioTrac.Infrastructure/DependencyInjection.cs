using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PhysioTrac.Application.Audit;
using PhysioTrac.Application.Billing;
using PhysioTrac.Application.Booking;
using PhysioTrac.Application.Clinical;
using PhysioTrac.Application.Configuration;
using PhysioTrac.Application.Consents;
using PhysioTrac.Application.Documents;
using PhysioTrac.Application.Scheduling;
using PhysioTrac.Application.Sessions;
using PhysioTrac.Application.SuperAdmin;
using PhysioTrac.Application.Tenancy;
using PhysioTrac.Infrastructure.Identity;
using PhysioTrac.Infrastructure.Persistence;
using PhysioTrac.Infrastructure.Services;

namespace PhysioTrac.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PhysioTracDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Default")));
        services.AddMemoryCache();
        services.AddHttpContextAccessor();
        services.AddScoped<CurrentUserAccessor>();

        services.Configure<SecurityOptions>(configuration.GetSection(SecurityOptions.SectionName));
        services.Configure<AppOptions>(configuration.GetSection(AppOptions.SectionName));
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));

        services.AddScoped<ITenantAccessService, TenantAccessService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IClientProvisioningService, ClientProvisioningService>();
        services.AddScoped<IPrivilegedAccessService, PrivilegedAccessService>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IAppointmentService, AppointmentService>();
        services.AddScoped<IClinicalNoteService, ClinicalNoteService>();
        services.AddScoped<IFunctionalGoalService, FunctionalGoalService>();
        services.AddScoped<IOutcomeScoreService, OutcomeScoreService>();
        services.AddScoped<IPublicBookingService, PublicBookingService>();
        services.AddScoped<IPortalBookingService, PortalBookingService>();
        services.AddScoped<IChargeService, ChargeService>();
        services.AddScoped<IClaimService, ClaimService>();
        services.AddScoped<IClaimTransactionService, ClaimTransactionService>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IConsentService, ConsentService>();

        return services;
    }
}
