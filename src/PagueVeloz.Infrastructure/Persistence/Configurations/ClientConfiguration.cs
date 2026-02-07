using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PagueVeloz.Domain.Entities;

namespace PagueVeloz.Infrastructure.Persistence.Configurations;

public sealed class ClientConfiguration : BaseEntityConfiguration<Client>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("clients");

        builder.HasIndex(c => c.ClientId).IsUnique();

        builder.Property(c => c.ClientId)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.HasMany(c => c.Accounts)
            .WithOne()
            .HasForeignKey(a => a.ClientId)
            .HasPrincipalKey(c => c.ClientId);
    }
}
