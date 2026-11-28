using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VideoProcessor.Core.Domain;

namespace VideoProcessor.Core.Data.Configurations;

internal sealed class VideoConfiguration : IEntityTypeConfiguration<Video>
{
    public void Configure(EntityTypeBuilder<Video> builder)
    {
        builder.HasKey(v => v.Id);

        builder.HasOne<User>().WithMany().HasForeignKey(v => v.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(v => new { v.UserId, v.CreatedAt });

        builder.Property(v => v.FileName).HasMaxLength(255);
        builder.Property(v => v.BlobName).HasMaxLength(64);
        builder.HasIndex(v => v.BlobName).IsUnique();

        builder.Property(v => v.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(v => v.RejectionReason).HasConversion<string>().HasMaxLength(32);

        // Optimistic concurrency on PostgreSQL's xmin, so two concurrent Confirms can't both win.
        builder.Property<uint>("Version").IsRowVersion();

        builder.OwnsOne(v => v.Metadata, metadata =>
        {
            metadata.Property(m => m.Container).HasMaxLength(32);
            metadata.Property(m => m.VideoCodec).HasMaxLength(32);
            metadata.Property(m => m.AudioCodec).HasMaxLength(32);
            metadata.Ignore(m => m.HasVideo);
            metadata.Ignore(m => m.HasAudio);
        });
    }
}
