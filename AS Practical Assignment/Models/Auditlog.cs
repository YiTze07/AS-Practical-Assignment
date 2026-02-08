using AS_Practical_Assignment.Models;
using System;
using System.ComponentModel.DataAnnotations;

namespace AS_Practical_Assignment.Models
{
    public class AuditLog
    {
        [Key]
        public int AuditLogId { get; set; }

        // Foreign key to Member
        public int MemberId { get; set; }
        public Member Member { get; set; }

        // What happened — e.g. "Login", "Logout", "Registration", "PasswordChange", "LoginFailed", "AccountLocked"
        [Required, StringLength(100)]
        public string Activity { get; set; }

        // When it happened
        [Required]
        public DateTime Timestamp { get; set; } = DateTime.Now;

        // Extra details (optional) — e.g. "Failed attempt 2 of 3"
        public string Details { get; set; }

        // IP address of the user at the time
        public string IpAddress { get; set; }
    }
}