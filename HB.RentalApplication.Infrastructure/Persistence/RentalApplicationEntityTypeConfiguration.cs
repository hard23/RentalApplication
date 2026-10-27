using HB.RentalApplication.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HB.RentalApplication.Infrastructure.Persistence;

public class RentalApplicationEntityTypeConfiguration : IEntityTypeConfiguration<HB.RentalApplication.Domain.RentalApplication>
{
    public void Configure(EntityTypeBuilder<HB.RentalApplication.Domain.RentalApplication> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.ApplicantFullName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.ApplicantEmail)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.PropertyAddress)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.AnnualIncome)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.HouseholdSize)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedAtUtc);
    }
}
