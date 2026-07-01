using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstate.Domain.Entities;
using RealEstate.Domain.Enums;

namespace RealEstate.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Email)
            .IsRequired()
            .HasMaxLength(320);

        builder.Property(user => user.NormalizedEmail)
            .IsRequired()
            .HasMaxLength(320);

        builder.HasIndex(user => user.NormalizedEmail)
            .IsUnique();

        builder.Property(user => user.PasswordHash)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(user => user.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(user => user.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(user => user.PhoneNumber)
            .HasMaxLength(50);

        builder.Property(user => user.Role)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(user => user.Status)
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(user => user.CreatedAtUtc)
            .IsRequired();

        builder.Property(user => user.ModifiedAtUtc);
    }
}
