using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace CCMW.DTOs
{
    public class LoginRequest
    {
        public string Email { get; set; }
        public string PasswordHash { get; set; } // Keep same name as your property
        public string UserType { get; set; } // optional
    }
}