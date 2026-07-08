using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RealEstate.Domain.Entities;

namespace RealEstate.Infrastructure.Persistence.Configurations;

public sealed class AgencyInvitationConfiguration : IEntityTypeConfiguration<AgencyInvitation>
{
    public void Configure(EntityTypeBuilder<AgencyInvitation> builder)
    {
        builder.ToTable("AgencyInvitations");

        builder.HasKey(invitation => invitation.Id);

        builder.Property(invitation => invitation.Email)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(invitation => invitation.NormalizedEmail)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(invitation => invitation.Token)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasIndex(invitation => invitation.Token)
            .IsUnique();

        builder.Property(invitation => invitation.Code)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(invitation => invitation.Role)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(invitation => invitation.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(invitation => invitation.ExpiresAtUtc)
            .IsRequired();

        builder.Property(invitation => invitation.AcceptedAtUtc);

        builder.Property(invitation => invitation.CancelledAtUtc);

        builder.Property(invitation => invitation.CreatedAtUtc)
            .IsRequired();

        builder.Property(invitation => invitation.ModifiedAtUtc);

        builder.HasOne(invitation => invitation.Agency)
            .WithMany()
            .HasForeignKey(invitation => invitation.AgencyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(invitation => invitation.InvitedByUser)
            .WithMany()
            .HasForeignKey(invitation => invitation.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(invitation => invitation.AcceptedByUser)
            .WithMany()
            .HasForeignKey(invitation => invitation.AcceptedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(invitation => invitation.AgencyId);

        builder.HasIndex(invitation => invitation.NormalizedEmail);

        builder.HasIndex(invitation => new
        {
            invitation.AgencyId,
            invitation.NormalizedEmail
        })
        .IsUnique()
        .HasFilter("\"Status\" = 'Pending'");
    }
}
