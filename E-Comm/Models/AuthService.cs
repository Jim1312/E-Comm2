using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace E_Comm.Models
{
    public class AuthService
    {
        private readonly EntertainmentGuildContext _context;

        public AuthService(EntertainmentGuildContext context)
        {
            _context = context;
        }

        // Test credentials from requirements document
        private readonly Dictionary<string, UserCredentials> _testCredentials = new()
        {
            {
                "customer@example.com", 
                new UserCredentials { Email = "customer@example.com", Password = "Password1", Role = "Customer", Name = "Cus Tomer" }
            },
            {
                "employee@example.com", 
                new UserCredentials { Email = "employee@example.com", Password = "Passw0rd", Role = "Employee", Name = "Empl Oyee" }
            },
            {
                "administrator@example.com", 
                new UserCredentials { Email = "administrator@example.com", Password = "Pa$$w0rd", Role = "Admin", Name = "Admin Istrator" }
            }
        };

        public async Task<AuthResult> AuthenticateAsync(string email, string password)
        {
            // First try test credentials for development
            if (_testCredentials.ContainsKey(email))
            {
                var testUser = _testCredentials[email];
                if (testUser.Password == password)
                {
                    return new AuthResult
                    {
                        IsSuccess = true,
                        Email = testUser.Email,
                        Name = testUser.Name,
                        Role = testUser.Role,
                        IsAdmin = testUser.Role == "Admin",
                        IsEmployee = testUser.Role == "Employee",
                        IsCustomer = testUser.Role == "Customer"
                    };
                }
            }

            // Then try database authentication
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user != null && VerifyPassword(password, user.Salt, user.HashPW))
            {
                return new AuthResult
                {
                    IsSuccess = true,
                    Email = user.Email ?? "",
                    Name = user.Name ?? "",
                    Role = user.IsAdmin ? "Admin" : "Employee",
                    IsAdmin = user.IsAdmin,
                    IsEmployee = !user.IsAdmin,
                    IsCustomer = false
                };
            }

            return new AuthResult { IsSuccess = false };
        }
        // NEW: Registration method
        public async Task<RegistrationResult> RegisterUserAsync(User user, string password)
        {
            try
            {
                // Check if email already exists in database
                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Email == user.Email);
                if (existingUser != null)
                {
                    return new RegistrationResult { IsSuccess = false, ErrorMessage = "Email is already registered." };
                }

                // Check if email exists in test credentials
                if (_testCredentials.ContainsKey(user.Email ?? ""))
                {
                    return new RegistrationResult { IsSuccess = false, ErrorMessage = "Email is already registered." };
                }

                // Check if username already exists
                var existingUsername = await _context.Users.FirstOrDefaultAsync(u => u.UserName == user.UserName);
                if (existingUsername != null)
                {
                    return new RegistrationResult { IsSuccess = false, ErrorMessage = "Username is already taken." };
                }

                // Generate salt and hash password
                user.Salt = GenerateSalt();
                user.HashPW = HashPassword(password, user.Salt);

                // Set default values for new users
                user.IsAdmin = false; // New users are customers by default

                // Add user to database
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                return new RegistrationResult { IsSuccess = true };
            }
            catch (Exception ex)
            {
                return new RegistrationResult { IsSuccess = false, ErrorMessage = "Registration failed. Please try again." };
            }
        }
        private bool VerifyPassword(string password, string? salt, string? hashedPassword)
        {
            if (string.IsNullOrEmpty(salt) || string.IsNullOrEmpty(hashedPassword))
                return false;

            var hash = HashPassword(password, salt);
            return hash == hashedPassword;
        }

        private string HashPassword(string password, string salt)
        {
            using (var sha256 = SHA256.Create())
            {
                var combinedBytes = Encoding.UTF8.GetBytes(password + salt);
                var hashBytes = sha256.ComputeHash(combinedBytes);
                return Convert.ToHexString(hashBytes).ToLower();
            }
        }

        // NEW: Generate salt for new users
        private string GenerateSalt()
        {
            var saltBytes = new byte[24];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }
            return Convert.ToBase64String(saltBytes);
        }
    }

    public class UserCredentials
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string Role { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public class AuthResult
    {
        public bool IsSuccess { get; set; }
        public string Email { get; set; } = "";
        public string Name { get; set; } = "";
        public string Role { get; set; } = "";
        public bool IsAdmin { get; set; }
        public bool IsEmployee { get; set; }
        public bool IsCustomer { get; set; }
    }

    // NEW: Registration result class
    public class RegistrationResult
    {
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
    }
}