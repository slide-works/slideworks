using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SlideWorks.WebApi.Features.Accounts;

namespace SlideWorks.WebApi.Features.DeviceSessions.Data;

// The device_sign_in_code table, keyed by the code's hash. SQLite ignores a column's length, so the device name's limit is
// a check.
public sealed class DeviceSignInCodeConfiguration : IEntityTypeConfiguration<DeviceSignInCode>
{
    public void Configure(EntityTypeBuilder<DeviceSignInCode> code)
    {
        code.HasKey(c => c.CodeHash);
        code.HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Restrict);
        code.HasIndex(c => c.UserId);
        code.Property(c => c.DeviceName).HasMaxLength(DeviceSession.DEVICE_NAME_MAX);
        code.ToTable(table => table.HasCheckConstraint(
            "CK_DeviceSignInCodes_DeviceName_Length", $"length(\"DeviceName\") <= {DeviceSession.DEVICE_NAME_MAX}"));
    }
}
