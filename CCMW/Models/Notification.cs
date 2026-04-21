using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CCMW.Models
{
    [Table("Notifications")]
    public class Notification
    {
        [Key]
        [Column("NotificationId")]
        public Guid NotificationId { get; set; }

        [Column("UserId")]
        public Guid UserId { get; set; }

        [Column("notification_type")]
        public string NotificationType { get; set; }

        [Column("title")]
        public string Title { get; set; }

        [Column("Message")]
        public string Message { get; set; }

        [Column("reference_type")]
        public string ReferenceType { get; set; }

        [Column("reference_id")]
        public Guid? ReferenceId { get; set; }

        [Column("IsRead")]
        public bool IsRead { get; set; }

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; }

        [Column("read_at")]
        public DateTime? ReadAt { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }
    }
}