using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstate.Domain.Entities;

namespace RealEstate.Infrastructure.Persistence.Configurations;

public sealed class AgencyMemberConfiguration : IEntityTypeConfiguration<AgencyMember>
{
    public void Configure(EntityTypeBuilder<AgencyMember> builder)
    {
        builder.ToTable("AgencyMembers");

        builder.HasKey(member => member.Id);

        builder.Property(member => member.Role)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(member => member.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(member => member.CreatedAtUtc)
            .IsRequired();

        builder.Property(member => member.ModifiedAtUtc);

        builder.HasOne(member => member.Agency)
            .WithMany(agency => agency.Members)
            .HasForeignKey(member => member.AgencyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(member => member.User)
            .WithMany()
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(member => member.AgencyId);

        builder.HasIndex(member => member.UserId);

        builder.HasIndex(member => new
        {
            member.AgencyId,
            member.UserId
        })
        .IsUnique();
    }
}
