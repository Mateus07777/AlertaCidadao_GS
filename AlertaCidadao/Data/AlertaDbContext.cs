using AlertaCidadao.Entities;
using Microsoft.EntityFrameworkCore;

namespace AlertaCidadao.Data;

public sealed class AlertaDbContext(DbContextOptions<AlertaDbContext> options) : DbContext(options)
{
    public DbSet<Citizen> Citizens => Set<Citizen>();
    public DbSet<OfficialAlert> OfficialAlerts => Set<OfficialAlert>();
    public DbSet<CitizenOccurrence> CitizenOccurrences => Set<CitizenOccurrence>();
    public DbSet<CitizenNotification> CitizenNotifications => Set<CitizenNotification>();
    public DbSet<MonitoringExecution> MonitoringExecutions => Set<MonitoringExecution>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Citizen>(e => { e.HasIndex(x => x.Email).IsUnique(); e.Property(x => x.Email).HasMaxLength(254).IsRequired(); e.Property(x => x.Name).HasMaxLength(150).IsRequired(); e.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired(); e.Property(x => x.Municipality).HasMaxLength(120).IsRequired(); e.Property(x => x.Role).HasMaxLength(20).IsRequired(); e.HasIndex(x => x.MunicipalityIbgeCode); });
        b.Entity<OfficialAlert>(e => { e.HasIndex(x => x.ExternalIdentifier).IsUnique(); e.HasIndex(x => new { x.MunicipalityIbgeCode, x.Severity }); e.Property(x => x.Title).HasMaxLength(200).IsRequired(); e.Property(x => x.EventType).HasMaxLength(100).IsRequired(); e.Property(x => x.Severity).HasMaxLength(40).IsRequired(); e.Property(x => x.Source).HasMaxLength(120).IsRequired(); e.Property(x => x.SourceUrl).HasMaxLength(300).IsRequired(); e.Property(x => x.AffectedMunicipalityCodes).HasColumnType("json").IsRequired(); });
        b.Entity<CitizenOccurrence>(e => { e.HasIndex(x => new { x.CitizenId, x.Status }); e.HasIndex(x => new { x.Municipality, x.Type, x.Severity }); e.Property(x => x.Title).HasMaxLength(200).IsRequired(); e.Property(x => x.Description).HasMaxLength(4000).IsRequired(); e.Property(x => x.Type).HasMaxLength(80).IsRequired(); e.Property(x => x.Severity).HasMaxLength(40).IsRequired(); e.Property(x => x.Status).HasMaxLength(30).IsRequired(); e.Property(x => x.Latitude).HasPrecision(10, 7); e.Property(x => x.Longitude).HasPrecision(10, 7); e.HasOne(x => x.Citizen).WithMany(x => x.Occurrences).HasForeignKey(x => x.CitizenId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<CitizenNotification>(e => { e.HasIndex(x => new { x.CitizenId, x.OfficialAlertId }).IsUnique(); e.Property(x => x.Title).HasMaxLength(200).IsRequired(); e.Property(x => x.Message).HasMaxLength(2000).IsRequired(); e.Property(x => x.Status).HasMaxLength(30).IsRequired(); e.HasOne(x => x.Citizen).WithMany(x => x.Notifications).HasForeignKey(x => x.CitizenId).OnDelete(DeleteBehavior.Restrict); e.HasOne(x => x.OfficialAlert).WithMany(x => x.Notifications).HasForeignKey(x => x.OfficialAlertId).OnDelete(DeleteBehavior.Restrict); });
        b.Entity<MonitoringExecution>(e => { e.HasIndex(x => x.StartedAt); e.Property(x => x.Provider).HasMaxLength(100).IsRequired(); e.Property(x => x.Status).HasMaxLength(30).IsRequired(); e.Property(x => x.ErrorMessage).HasMaxLength(4000); });
        b.Entity<IdempotencyRecord>(e => { e.HasIndex(x => x.IdempotencyKey).IsUnique(); e.HasIndex(x => x.ExpiresAt); e.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired(); e.Property(x => x.RequestHash).HasMaxLength(64).IsRequired(); e.Property(x => x.ResponseBody).HasColumnType("longtext").IsRequired(); });
    }
}
