using AS_Practical_Assignment.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AS_Practical_Assignment.Models
{
    public class Member
    {
        [Key]
        public int MemberId { get; set; }

        [Required, StringLength(50)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required, StringLength(50)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required]
        [Display(Name = "Gender")]
        public string Gender { get; set; } // "Male" or "Female"

        // NRIC is stored ENCRYPTED in the database
        [Required]
        [Display(Name = "NRIC (Encrypted)")]
        public string NricEncrypted { get; set; }

        [Required, EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; } // Must be unique — enforced in DbContext

        // Password is HASHED before saving
        [Required]
        [Display(Name = "Password Hash")]
        public string PasswordHash { get; set; }

        // Salt used for password hashing
        [Required]
        public string PasswordSalt { get; set; }

        [Required]
        [Display(Name = "Date of Birth")]
        [DataType(DataType.Date)]
        public DateTime DateOfBirth { get; set; }

        // Stores the file path of the uploaded resume (.docx or .pdf)
        [Display(Name = "Resume File Path")]
        public string? ResumePath { get; set; }

        // Free text field — allows all special characters
        [Display(Name = "Who Am I")]
        public string? WhoAmI { get; set; }

        // --- Account Lockout Fields ---
        public int FailedLoginAttempts { get; set; } = 0;
        public bool IsLocked { get; set; } = false;
        public DateTime? LockoutExpiryTime { get; set; } // auto-unlock after x mins

        // --- Password Policy Fields ---
        public DateTime? LastPasswordChangeDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // --- Password Reset Fields ---
        public string? PasswordResetToken { get; set; }
        public DateTime? PasswordResetTokenExpiry { get; set; }

        // --- Session Token (for detecting multiple logins) ---
        public string? SessionToken { get; set; }

        // --- Navigation Properties (relationships) ---
        public ICollection<AuditLog> AuditLogs { get; set; }
        public ICollection<PasswordHistory> PasswordHistories { get; set; }
    }
}