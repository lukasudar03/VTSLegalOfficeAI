using Microsoft.EntityFrameworkCore;
using VTSLegalOfficeAI.Entities;

namespace VTSLegalOfficeAI.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Document> Documents { get; set; }
        public DbSet<DocumentChunk> DocumentChunks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Document>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.FileName)
                    .IsRequired()
                    .HasMaxLength(260);

                entity.Property(x => x.StoredFileName)
                    .IsRequired()
                    .HasMaxLength(260);

                entity.Property(x => x.FilePath)
                    .IsRequired()
                    .HasMaxLength(500);

                entity.Property(x => x.Status)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(x => x.ExtractedText)
                    .HasColumnType("text");
            });

            modelBuilder.Entity<DocumentChunk>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Content)
                    .IsRequired();

                entity.Property(x => x.Embedding)
                    .HasColumnType("vector(768)");

                entity.HasOne(x => x.Document)
                    .WithMany(x => x.Chunks)
                    .HasForeignKey(x => x.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}