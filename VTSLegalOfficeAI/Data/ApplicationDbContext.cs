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

        public DbSet<User> Users { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<DocumentChunk> DocumentChunks { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Username)
                    .IsRequired()
                    .HasMaxLength(100);

                entity.HasIndex(x => x.Username)
                    .IsUnique();

                entity.Property(x => x.Email)
                    .IsRequired()
                    .HasMaxLength(320);

                entity.HasIndex(x => x.Email)
                    .IsUnique();

                entity.Property(x => x.PasswordHash)
                    .IsRequired();
            });

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

                entity.HasOne(x => x.User)
                    .WithMany(x => x.Documents)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
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

            modelBuilder.Entity<ChatMessage>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Question)
                    .IsRequired();

                entity.Property(x => x.Answer)
                    .IsRequired();

                entity.Property(x => x.SourcesJson)
                    .HasColumnType("jsonb")
                    .HasDefaultValue("[]");

                entity.HasOne(x => x.Document)
                    .WithMany(x => x.ChatMessages)
                    .HasForeignKey(x => x.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
