using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PagueVeloz.Domain.Entities;
using PagueVeloz.Domain.Enums;

namespace PagueVeloz.Infrastructure.Persistence.Configurations;

public sealed class AccountConfiguration : BaseEntityConfiguration<Account>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");

        builder.HasIndex(a => a.AccountId).IsUnique();

        builder.Property(a => a.AccountId)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.ClientId)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(a => a.Balance)
            .IsRequired();

        builder.Property(a => a.ReservedBalance)
            .IsRequired();

        builder.Property(a => a.CreditLimit)
            .IsRequired();

        builder.Property(a => a.Status)
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<AccountStatus>(v, true))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.CurrencyCode)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(a => a.Version)
            .IsConcurrencyToken();

        builder.HasMany(a => a.Transactions)
            .WithOne()
            .HasForeignKey(t => t.AccountId)
            .HasPrincipalKey(a => a.AccountId);
    }
}
