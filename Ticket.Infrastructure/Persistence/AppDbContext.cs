using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Ticket.Infrastructure.Entities;
using Ticket.Infrastructure.Identity;

namespace Ticket.Infrastructure.Persistence
{
    public class AppDbContext: IdentityDbContext<AppUser, AppRole, Guid>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // Ticket domain tabloları:
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<CustomerContact> CustomerContacts => Set<CustomerContact>();
        public DbSet<Problem> Problems => Set<Problem>();
        public DbSet<TicketItem> Tickets => Set<TicketItem>();
        public DbSet<TicketActivity> TicketActivities => Set<TicketActivity>();
        public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
        public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
        public DbSet<UserPermissionGrant> UserPermissionGrants => Set<UserPermissionGrant>();
        public DbSet<UserPermissionDeny> UserPermissionDenies => Set<UserPermissionDeny>();
        public DbSet<EmailIngestSettings> EmailIngestSettings => Set<EmailIngestSettings>();
        public DbSet<EmailIngestReceipt> EmailIngestReceipts => Set<EmailIngestReceipt>();





        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);

            // Customer
            b.Entity<Customer>(e =>
            {
                e.HasKey(x => x.CustomerId);
                e.Property(x => x.Code).HasMaxLength(50).IsRequired();
                e.Property(x => x.Title).HasMaxLength(200).IsRequired();
                e.HasIndex(x => x.Code).IsUnique();
            });

            b.Entity<CustomerContact>(e =>
            {
                e.HasKey(x => x.CustomerContactId);

                e.Property(x => x.FullName).HasMaxLength(150).IsRequired();
                e.Property(x => x.Email).HasMaxLength(200);
                e.Property(x => x.Phone).HasMaxLength(50);
                e.Property(x => x.Mobile).HasMaxLength(50);

                
                e.Property(x => x.IsActive).HasDefaultValue(true);
                e.Property(x => x.AllowEmailIngest).HasDefaultValue(true);

                e.HasOne(x => x.Customer)
                 .WithMany(x => x.Contacts)
                 .HasForeignKey(x => x.CustomerId)
                 .OnDelete(DeleteBehavior.Cascade);

                
            });


            // Problem
            b.Entity<Problem>(e =>
            {
                e.HasKey(x => x.ProblemId);
                e.Property(x => x.Code).HasMaxLength(50).IsRequired();
                e.Property(x => x.Name).HasMaxLength(200).IsRequired();
                e.Property(x => x.Department).HasMaxLength(100).IsRequired();
                e.HasIndex(x => x.Code).IsUnique();
            });

            // Ticket
            b.Entity<TicketItem>(e =>
            {
                e.HasKey(x => x.TicketItemId);
                e.Property(x => x.TicketNo).HasMaxLength(30).IsRequired();
                e.HasIndex(x => x.TicketNo).IsUnique();

                e.Property(x => x.Subject).HasMaxLength(300).IsRequired();
                e.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
                e.Property(x => x.Channel).HasConversion<string>().HasMaxLength(40);

                e.HasOne(x => x.Customer)
                 .WithMany()
                 .HasForeignKey(x => x.CustomerId)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.Problem)
                 .WithMany()
                 .HasForeignKey(x => x.ProblemId)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasMany(x => x.Activities)
                 .WithOne(x => x.TicketItem)
                 .HasForeignKey(x => x.TicketItemId);

                e.HasMany(x => x.Messages)
                 .WithOne(x => x.TicketItem)
                 .HasForeignKey(x => x.TicketItemId);
            });

            b.Entity<TicketMessage>(e =>
            {
                e.HasKey(x => x.TicketMessageId);
                e.Property(x => x.Body).IsRequired();

                e.HasIndex(x => x.ExternalMessageId);

                e.Property(x => x.InternetMessageId).HasMaxLength(450);
                e.Property(x => x.InReplyToInternetMessageId).HasMaxLength(450);

                e.HasIndex(x => x.InternetMessageId).HasFilter("[InternetMessageId] IS NOT NULL");
                e.HasIndex(x => x.InReplyToInternetMessageId).HasFilter("[InReplyToInternetMessageId] IS NOT NULL");
            });


            b.Entity<TicketActivity>(e =>
            {
                e.HasKey(x => x.TicketActivityId);
                e.Property(x => x.Type).HasMaxLength(80).IsRequired();
                e.Property(x => x.Note).HasMaxLength(2000).IsRequired();
            });

            b.Entity<Permission>(b =>
            {
                b.HasKey(x => x.Id);
                b.Property(x => x.Key).HasMaxLength(200).IsRequired();
                b.HasIndex(x => x.Key).IsUnique();
                b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            });

            b.Entity<RolePermission>(b =>
            {
                b.HasKey(x => new { x.RoleId, x.PermissionKey }); // composite key
                b.Property(x => x.PermissionKey).HasMaxLength(200).IsRequired();
                b.HasOne(x => x.Role)
                    .WithMany()
                    .HasForeignKey(x => x.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            b.Entity<UserPermission>(b =>
            {
                b.HasKey(x => new { x.UserId, x.PermissionKey }); // composite key
                b.Property(x => x.PermissionKey).HasMaxLength(200).IsRequired();

                b.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            b.Entity<UserPermissionGrant>(b =>
            {
                b.HasKey(x => new { x.UserId, x.PermissionKey });
                b.Property(x => x.PermissionKey).HasMaxLength(200).IsRequired();
                b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            b.Entity<UserPermissionDeny>(b =>
            {
                b.HasKey(x => new { x.UserId, x.PermissionKey });
                b.Property(x => x.PermissionKey).HasMaxLength(200).IsRequired();
                b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            });

            b.Entity<EmailIngestSettings>(e =>
            {
                e.HasKey(x => x.Id);

                e.Property(x => x.TargetAddress).HasMaxLength(200);

                e.Property(x => x.Protocol).HasConversion<string>().HasMaxLength(20);

                e.Property(x => x.ImapHost).HasMaxLength(200);
                e.Property(x => x.ImapUserName).HasMaxLength(200);
                e.Property(x => x.EncryptedImapPassword).HasMaxLength(2000);
                e.Property(x => x.Folder).HasMaxLength(100);

                e.Property(x => x.Pop3Host).HasMaxLength(200);
                e.Property(x => x.Pop3UserName).HasMaxLength(200);
                e.Property(x => x.EncryptedPop3Password).HasMaxLength(2000);
            });

            // EmailIngestReceipt
            b.Entity<EmailIngestReceipt>(e =>
            {
                e.HasKey(x => x.Id);

                e.Property(x => x.ExternalMessageId).HasMaxLength(400).IsRequired();
                e.HasIndex(x => x.ExternalMessageId).IsUnique();

                // ✅ Seçenek A: Message-Id dedupe desteği
                e.Property(x => x.InternetMessageId).HasMaxLength(400);

                // Nullable olduğu için SQL Server’da birden çok NULL’a izin verir,
                // ama yine de temiz olması için filtered unique index öneriyorum:
                e.HasIndex(x => x.InternetMessageId)
                    .IsUnique()
                    .HasFilter("[InternetMessageId] IS NOT NULL");

                e.Property(x => x.Status).HasMaxLength(60).IsRequired();
                e.Property(x => x.From).HasMaxLength(200);
                e.Property(x => x.Subject).HasMaxLength(500);
                
            });






        }
    }
}
