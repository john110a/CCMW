using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;



namespace CCMW.DTOs
{
    public class RegisterRequest
    {
        public string Email { get; set; }
        public string PasswordHash { get; set; } // Keep same name
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string CNIC { get; set; } // Keep CNIC (not Cnic) to match your User entity
        public string Address { get; set; }
        public Guid? ZoneId { get; set; }
        public string UserType { get;  set; }
    }
}