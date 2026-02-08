using AS_Practical_Assignment.Models;
using System;
using System.ComponentModel.DataAnnotations;

namespace AS_Practical_Assignment.Models
{
    public class PasswordHistory
    {
        [Key]
        public int PasswordHistoryId { get; set; }

        // Foreign key to Member
        public int MemberId { get; set; }
        public Member Member { get; set; }

        // Store the old hashed password — used to prevent reuse (max 2 history)
        [Required]
        public string OldPasswordHash { get; set; }

        [Required]
        public string OldPasswordSalt { get; set; }

        // When this password was set
        [Required]
        public DateTime ChangedAt { get; set; } = DateTime.Now;
    }
}