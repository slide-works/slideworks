using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SlideWorks.WebApi.Features.Accounts;

namespace SlideWorks.WebApi.Features.DeviceSessions.Data;

// The device_session table. The end reason is stored by name and a check holds it to the enum's names; SQLite ignores a
// column's length, so the device name's limit is a check. People are never deleted, so the user key is Restrict.
public sealed class DeviceSessionConfiguration : IEntityTypeConfiguration<DeviceSession>
{
    public void Configure(EntityTypeBuilder<DeviceSession> session)
    {
        session.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Restrict);
        session.HasIndex(s => s.UserId);
        session.HasIndex(s => s.SecretHash).IsUnique();
        session.Property(s => s.DeviceName).HasMaxLength(DeviceSession.DEVICE_NAME_MAX);
        session.Property(s => s.EndReason).HasConversion<string>();
        var reasons = string.Join(", ", Enum.GetNames<DeviceSessionEndReason>().Select(name => $"'{name}'"));
        session.ToTable(table =>
        {
            table.HasCheckConstraint("CK_DeviceSessions_DeviceName_Length", $"length(\"DeviceName\") <= {DeviceSession.DEVICE_NAME_MAX}");
            table.HasCheckConstraint("CK_DeviceSessions_EndReason", $"\"EndReason\" IS NULL OR \"EndReason\" IN ({reasons})");
        });
    }
}
