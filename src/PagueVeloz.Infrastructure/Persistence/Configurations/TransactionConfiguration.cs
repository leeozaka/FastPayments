using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PagueVeloz.Domain.Entities;
using PagueVeloz.Domain.Enums;

namespace PagueVeloz.Infrastructure.Persistence.Configurations;

public sealed class TransactionConfiguration : BaseEntityConfiguration<Transaction>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions");

        builder.HasIndex(t => t.ReferenceId).IsUnique();
        builder.HasIndex(t => t.AccountId);
        builder.HasIndex(t => t.Timestamp);

        builder.Property(t => t.AccountId)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.Type)
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<TransactionType>(v, true))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.Amount)
            .IsRequired();

        builder.Property(t => t.CurrencyCode)
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(t => t.ReferenceId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.Status)
            .HasConversion(
                v => v.ToString().ToLowerInvariant(),
                v => Enum.Parse<TransactionStatus>(v, true))
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.ErrorMessage)
            .HasMaxLength(500);

        builder.Property(t => t.RelatedReferenceId)
            .HasMaxLength(100);

        builder.Property(t => t.Metadata)
            .HasConversion(
                v => v != null ? System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null) : null,
                v => v != null ? System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v, (System.Text.Json.JsonSerializerOptions?)null) : null)
            .HasMaxLength(4000);

        builder.Property(t => t.Timestamp).IsRequired();
    }
}
