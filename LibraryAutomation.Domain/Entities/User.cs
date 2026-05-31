using System;
using System.Collections.Generic;
using System.Text;

namespace LibraryAutomation.Domain.Entities
{
    public enum UserRole
    {
        Admin = 1,
        Member = 2
    }

    public class User
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.Member;

        public ICollection<Loan> Loans { get; set; } = new List<Loan>();
    }
}
