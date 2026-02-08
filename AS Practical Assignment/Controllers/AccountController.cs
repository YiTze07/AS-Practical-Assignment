using AS_Practical_Assignment.Data;
using AS_Practical_Assignment.Models;
using AS_Practical_Assignment.Services;
using AS_Practical_Assignment.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AS_Practical_Assignment.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;
        private readonly EncryptionHelper _encryptionHelper;
        private readonly RecaptchaService _recaptchaService;
        private readonly EmailService _emailService;
        private readonly IConfiguration _configuration;

        private const int LockoutDurationMinutes = 15;
        private const int MaxFailedAttempts = 3;
        private const int MinPasswordAgeMinutes = 15;
        private const int MaxPasswordAgeDays = 90;

        public AccountController(
            AppDbContext context,
            EncryptionHelper encryptionHelper,
            RecaptchaService recaptchaService,
            EmailService emailService,
            IConfiguration configuration)
        {
            _context = context;
            _encryptionHelper = encryptionHelper;
            _recaptchaService = recaptchaService;
            _emailService = emailService;
            _configuration = configuration;
        }

        // ===================== REGISTER =====================

        [HttpGet]
        public IActionResult Register()
        {
            if (HttpContext.Session.GetString("MemberId") != null)
                return RedirectToAction("Index", "Home");
            ViewData["ReCaptchaSiteKey"] = _configuration["Google:ReCaptcha:SiteKey"];
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            ViewData["ReCaptchaSiteKey"] = _configuration["Google:ReCaptcha:SiteKey"];

            // reCAPTCHA v3
            bool captchaValid = await _recaptchaService.ValidateAsync(model.RecaptchaToken);
            if (!captchaValid)
            {
                ModelState.AddModelError("", "reCAPTCHA validation failed. Please try again.");
                return View(model);
            }

            // Server-side password complexity
            if (!PasswordHelper.ValidatePasswordComplexity(model.Password))
            {
                ModelState.AddModelError("Password", "Password must be at least 12 characters with uppercase, lowercase, number and special character.");
                return View(model);
            }

            if (!System.Text.RegularExpressions.Regex.IsMatch(model.Nric, @"^[A-Z]\d{7}[A-Z]$"))
            {
                ModelState.AddModelError("Nric", "NRIC must be in format: 1 letter, 7 digits, 1 letter (e.g., S1234567A)");
                return View(model);
            }

            if (ModelState.IsValid)
            {
                // Duplicate email check
                if (await _context.Members.AnyAsync(m => m.Email == model.Email))
                {
                    ModelState.AddModelError("Email", "This email is already registered.");
                    return View(model);
                }

                // Encrypt NRIC
                string encryptedNric = _encryptionHelper.Encrypt(model.Nric);

                // Hash password
                string salt = PasswordHelper.GenerateSalt();
                string hash = PasswordHelper.HashPassword(model.Password, salt);

                // Resume upload
                string resumePath = null;
                if (model.Resume != null && model.Resume.Length > 0)
                {
                    string ext = Path.GetExtension(model.Resume.FileName).ToLower();
                    if (ext != ".docx" && ext != ".pdf")
                    {
                        ModelState.AddModelError("Resume", "Only .docx or .pdf files are allowed.");
                        return View(model);
                    }
                    string uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Resumes");
                    Directory.CreateDirectory(uploadDir);
                    string fileName = Guid.NewGuid().ToString() + ext;
                    using var stream = new FileStream(Path.Combine(uploadDir, fileName), FileMode.Create);
                    await model.Resume.CopyToAsync(stream);
                    resumePath = "/Resumes/" + fileName;
                }

                // Encode WhoAmI to prevent XSS
                string encodedWhoAmI = string.IsNullOrEmpty(model.WhoAmI)
                    ? ""
                    : System.Net.WebUtility.HtmlEncode(model.WhoAmI);

                var member = new Member
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Gender = model.Gender,
                    NricEncrypted = encryptedNric,
                    Email = model.Email,
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    DateOfBirth = model.DateOfBirth,
                    ResumePath = resumePath,
                    WhoAmI = encodedWhoAmI,
                    LastPasswordChangeDate = DateTime.Now,
                    CreatedAt = DateTime.Now
                };

                _context.Members.Add(member);
                await _context.SaveChangesAsync();

                await LogActivity(member.MemberId, "Registration", "New account registered.");

                TempData["SuccessMessage"] = "Registration successful! Please login.";
                return RedirectToAction("Login");
            }
            return View(model);
        }

        // ===================== LOGIN =====================

        [HttpGet]
        public IActionResult Login()
        {
            if (HttpContext.Session.GetString("MemberId") != null)
                return RedirectToAction("Index", "Home");
            ViewData["ReCaptchaSiteKey"] = _configuration["Google:ReCaptcha:SiteKey"];
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            ViewData["ReCaptchaSiteKey"] = _configuration["Google:ReCaptcha:SiteKey"];

            bool captchaValid = await _recaptchaService.ValidateAsync(model.RecaptchaToken);
            if (!captchaValid)
            {
                ModelState.AddModelError("", "reCAPTCHA validation failed.");
                return View(model);
            }

            if (ModelState.IsValid)
            {
                var member = await _context.Members.FirstOrDefaultAsync(m => m.Email == model.Email);

                // Generic error message - don't reveal if email exists or not
                if (member == null)
                {
                    TempData["ErrorMessage"] = "Login failed. Please check your credentials and try again.";
                    return View(new LoginViewModel()); // Return empty model to clear fields
                }

                // Check if account is locked
                if (member.IsLocked)
                {
                    if (member.LockoutExpiryTime.HasValue && DateTime.Now >= member.LockoutExpiryTime.Value)
                    {
                        member.IsLocked = false;
                        member.FailedLoginAttempts = 0;
                        member.LockoutExpiryTime = null;
                        await _context.SaveChangesAsync();
                        await LogActivity(member.MemberId, "AccountUnlocked", "Account auto-unlocked.");
                    }
                    else
                    {
                        var remaining = member.LockoutExpiryTime.Value - DateTime.Now;
                        TempData["ErrorMessage"] = $"Account is locked. Try again in {(int)remaining.TotalMinutes + 1} minute(s).";
                        return View(new LoginViewModel());
                    }
                }

                // Verify password
                if (!PasswordHelper.VerifyPassword(model.Password, member.PasswordSalt, member.PasswordHash))
                {
                    member.FailedLoginAttempts++;
                    await LogActivity(member.MemberId, "LoginFailed", $"Failed attempt {member.FailedLoginAttempts} of {MaxFailedAttempts}.");

                    if (member.FailedLoginAttempts >= MaxFailedAttempts)
                    {
                        member.IsLocked = true;
                        member.LockoutExpiryTime = DateTime.Now.AddMinutes(LockoutDurationMinutes);
                        await _context.SaveChangesAsync();
                        await LogActivity(member.MemberId, "AccountLocked", $"Locked for {LockoutDurationMinutes} mins.");
                        TempData["ErrorMessage"] = $"Account locked for {LockoutDurationMinutes} minutes due to multiple failed login attempts.";
                    }
                    else
                    {
                        await _context.SaveChangesAsync();
                        TempData["ErrorMessage"] = "Login failed. Please check your credentials and try again.";
                    }
                    return View(new LoginViewModel()); // Clear fields
                }

                // Reset failed attempts
                member.FailedLoginAttempts = 0;
                member.IsLocked = false;
                await _context.SaveChangesAsync();

                // --- 2FA: Send OTP ---
                string otp = GenerateOTP();
                HttpContext.Session.SetString("PendingMemberId", member.MemberId.ToString());
                HttpContext.Session.SetString("OTP", otp);
                HttpContext.Session.SetString("OTPExpiry", DateTime.Now.AddMinutes(5).ToString());

                await _emailService.SendEmailAsync(
                    member.Email,
                    "Your Verification Code - Ace Job Agency",
                    $@"<div style='font-family:Segoe UI,sans-serif;max-width:400px;margin:0 auto;padding:30px;border:1px solid #e2e8f0;border-radius:8px;'>
                <h2 style='color:#1a365d;text-align:center;'>Ace Job Agency</h2>
                <p style='text-align:center;color:#4a5568;'>Your one-time verification code is:</p>
                <h1 style='text-align:center;color:#2b6cb0;letter-spacing:8px;'>{otp}</h1>
                <p style='text-align:center;color:#718096;font-size:0.85rem;'>This code expires in 5 minutes.</p>
            </div>");

                await LogActivity(member.MemberId, "OTPSent", "2FA OTP sent to email.");
                return RedirectToAction("VerifyOTP");
            }
            return View(model);
        }

        // ===================== 2FA - VERIFY OTP =====================

        [HttpGet]
        public IActionResult VerifyOTP()
        {
            if (HttpContext.Session.GetString("PendingMemberId") == null)
                return RedirectToAction("Login");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOTP(string otp)
        {
            string pendingId = HttpContext.Session.GetString("PendingMemberId");
            if (pendingId == null)
                return RedirectToAction("Login");

            string savedOtp = HttpContext.Session.GetString("OTP");
            string expiryStr = HttpContext.Session.GetString("OTPExpiry");

            // Check OTP expiry
            if (DateTime.Now > DateTime.Parse(expiryStr))
            {
                HttpContext.Session.Remove("PendingMemberId");
                HttpContext.Session.Remove("OTP");
                HttpContext.Session.Remove("OTPExpiry");
                TempData["ErrorMessage"] = "OTP has expired. Please login again.";
                return RedirectToAction("Login");
            }

            if (otp != savedOtp)
            {
                ViewData["ErrorMessage"] = "Invalid OTP code. Please try again.";
                return View();
            }

            // OTP valid — create full session
            int memberId = int.Parse(pendingId);
            var member = await _context.Members.FindAsync(memberId);

            // Generate unique session token (for multi-login detection)
            string sessionToken = Guid.NewGuid().ToString();
            member.SessionToken = sessionToken;
            await _context.SaveChangesAsync();

            HttpContext.Session.SetString("MemberId", memberId.ToString());
            HttpContext.Session.SetString("SessionToken", sessionToken);
            HttpContext.Session.SetString("MemberName", $"{member.FirstName} {member.LastName}");
            HttpContext.Session.SetString("LastActivity", DateTime.Now.ToString());

            // Clear OTP data
            HttpContext.Session.Remove("PendingMemberId");
            HttpContext.Session.Remove("OTP");
            HttpContext.Session.Remove("OTPExpiry");

            await LogActivity(memberId, "Login", "Successful login with 2FA.");
            return RedirectToAction("Index", "Home");
        }

        // Resend OTP
        [HttpPost]
        public async Task<IActionResult> ResendOTP()
        {
            string pendingId = HttpContext.Session.GetString("PendingMemberId");
            if (pendingId == null)
                return RedirectToAction("Login");

            int memberId = int.Parse(pendingId);
            var member = await _context.Members.FindAsync(memberId);

            string otp = GenerateOTP();
            HttpContext.Session.SetString("OTP", otp);
            HttpContext.Session.SetString("OTPExpiry", DateTime.Now.AddMinutes(5).ToString());

            await _emailService.SendEmailAsync(
                member.Email,
                "Your New Verification Code - Ace Job Agency",
                $@"<div style='font-family:Segoe UI,sans-serif;max-width:400px;margin:0 auto;padding:30px;border:1px solid #e2e8f0;border-radius:8px;'>
                    <h2 style='color:#1a365d;text-align:center;'>Ace Job Agency</h2>
                    <p style='text-align:center;color:#4a5568;'>Your new verification code is:</p>
                    <h1 style='text-align:center;color:#2b6cb0;letter-spacing:8px;'>{otp}</h1>
                    <p style='text-align:center;color:#718096;font-size:0.85rem;'>This code expires in 5 minutes.</p>
                </div>");

            TempData["SuccessMessage"] = "A new OTP has been sent to your email.";
            return RedirectToAction("VerifyOTP");
        }

        // ===================== LOGOUT =====================

        public async Task<IActionResult> Logout()
        {
            string memberIdStr = HttpContext.Session.GetString("MemberId");
            if (memberIdStr != null)
            {
                int memberId = int.Parse(memberIdStr);
                var member = await _context.Members.FindAsync(memberId);
                if (member != null)
                {
                    member.SessionToken = null;
                    await _context.SaveChangesAsync();
                }
                await LogActivity(memberId, "Logout", "User logged out.");
            }

            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        // ===================== CHANGE PASSWORD =====================

        [HttpGet]
        public async Task<IActionResult> ChangePassword()
        {
            string memberIdStr = HttpContext.Session.GetString("MemberId");
            if (memberIdStr == null)
                return RedirectToAction("Login");

            // Multi-login detection
            int memberId = int.Parse(memberIdStr);
            var member = await _context.Members.FindAsync(memberId);
            string currentSessionToken = HttpContext.Session.GetString("SessionToken");

            if (member.SessionToken != currentSessionToken)
            {
                HttpContext.Session.Clear();
                TempData["ErrorMessage"] = "Your account has been logged in from another device.";
                return RedirectToAction("Login");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (HttpContext.Session.GetString("MemberId") == null)
                return RedirectToAction("Login");

            if (ModelState.IsValid)
            {
                int memberId = int.Parse(HttpContext.Session.GetString("MemberId"));
                var member = await _context.Members.FindAsync(memberId);

                // Verify current password
                if (!PasswordHelper.VerifyPassword(model.CurrentPassword, member.PasswordSalt, member.PasswordHash))
                {
                    ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
                    return View(model);
                }

                // Check if new password is same as current password
                if (model.NewPassword == model.CurrentPassword)
                {
                    ModelState.AddModelError("NewPassword", "New password cannot be the same as your current password.");
                    return View(model);
                }


                // Min password age check
                if (member.LastPasswordChangeDate.HasValue &&
                    (DateTime.Now - member.LastPasswordChangeDate.Value).TotalMinutes < MinPasswordAgeMinutes)
                {
                    ModelState.AddModelError("", $"You must wait at least {MinPasswordAgeMinutes} minutes before changing your password again.");
                    return View(model);
                }

                // Server-side password complexity
                if (!PasswordHelper.ValidatePasswordComplexity(model.NewPassword))
                {
                    ModelState.AddModelError("NewPassword", "Password must be at least 12 characters with uppercase, lowercase, number and special character.");
                    return View(model);
                }

                // Check password reuse (max 2 history)
                var passwordHistories = await _context.PasswordHistories
                    .Where(ph => ph.MemberId == memberId)
                    .OrderByDescending(ph => ph.ChangedAt)
                    .Take(2)
                    .ToListAsync();

                foreach (var history in passwordHistories)
                {
                    if (PasswordHelper.VerifyPassword(model.NewPassword, history.OldPasswordSalt, history.OldPasswordHash))
                    {
                        ModelState.AddModelError("NewPassword", "You cannot reuse one of your last 2 passwords.");
                        return View(model);
                    }
                }

                // Save current password to history
                _context.PasswordHistories.Add(new PasswordHistory
                {
                    MemberId = memberId,
                    OldPasswordHash = member.PasswordHash,
                    OldPasswordSalt = member.PasswordSalt,
                    ChangedAt = DateTime.Now
                });

                // Update password
                string newSalt = PasswordHelper.GenerateSalt();
                string newHash = PasswordHelper.HashPassword(model.NewPassword, newSalt);
                member.PasswordHash = newHash;
                member.PasswordSalt = newSalt;
                member.LastPasswordChangeDate = DateTime.Now;

                await _context.SaveChangesAsync();
              
                await LogActivity(memberId, "PasswordChanged", "Password was changed.");

                TempData["SuccessMessage"] = "Password changed successfully.";
                return RedirectToAction("Index", "Home");
            }
            return View(model);
        }

        // ===================== FORGOT PASSWORD =====================

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                ModelState.AddModelError("email", "Email is required.");
                return View();
            }

            var member = await _context.Members.FirstOrDefaultAsync(m => m.Email == email);

            // Always show success message even if email not found (prevents user enumeration)
            if (member != null)
            {
                string token = Guid.NewGuid().ToString();
                member.PasswordResetToken = token;
                member.PasswordResetTokenExpiry = DateTime.Now.AddHours(1);
                await _context.SaveChangesAsync();

                string resetUrl = Url.Action("ResetPassword", "Account", new { token = token }, protocol: HttpContext.Request.Scheme);

                await _emailService.SendEmailAsync(
                    member.Email,
                    "Reset Your Password - Ace Job Agency",
                    $@"<div style='font-family:Segoe UI,sans-serif;max-width:500px;margin:0 auto;padding:30px;border:1px solid #e2e8f0;border-radius:8px;'>
                        <h2 style='color:#1a365d;text-align:center;'>Ace Job Agency</h2>
                        <p style='color:#4a5568;'>Hello {member.FirstName},</p>
                        <p style='color:#4a5568;'>Click the link below to reset your password. The link expires in 1 hour.</p>
                        <div style='text-align:center;margin:25px 0;'>
                            <a href='{resetUrl}' style='background:#2b6cb0;color:white;padding:12px 30px;border-radius:6px;text-decoration:none;font-size:1rem;'>Reset Password</a>
                        </div>
                        <p style='color:#718096;font-size:0.85rem;'>If you did not request this, please ignore this email.</p>
                    </div>");

                await LogActivity(member.MemberId, "PasswordResetRequested", "Password reset email sent.");
            }

            TempData["SuccessMessage"] = "If your email is registered, you will receive a password reset link.";
            return RedirectToAction("Login");
        }

        // ===================== RESET PASSWORD =====================

        [HttpGet]
        public async Task<IActionResult> ResetPassword(string token)
        {
            if (string.IsNullOrEmpty(token))
                return RedirectToAction("Login");

            var member = await _context.Members.FirstOrDefaultAsync(m => m.PasswordResetToken == token);
            if (member == null || member.PasswordResetTokenExpiry < DateTime.Now)
            {
                TempData["ErrorMessage"] = "Invalid or expired reset link.";
                return RedirectToAction("Login");
            }

            return View(new ResetPasswordViewModel { Token = token });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var member = await _context.Members.FirstOrDefaultAsync(m => m.PasswordResetToken == model.Token);
                if (member == null || member.PasswordResetTokenExpiry < DateTime.Now)
                {
                    TempData["ErrorMessage"] = "Invalid or expired reset link.";
                    return RedirectToAction("Login");
                }

                // Server-side password complexity
                if (!PasswordHelper.ValidatePasswordComplexity(model.Password))
                {
                    ModelState.AddModelError("Password", "Password must be at least 12 characters with uppercase, lowercase, number and special character.");
                    return View(model);
                }


                // Check password reuse
                var passwordHistories = await _context.PasswordHistories
                    .Where(ph => ph.MemberId == member.MemberId)
                    .OrderByDescending(ph => ph.ChangedAt)
                    .Take(2)
                    .ToListAsync();

                foreach (var history in passwordHistories)
                {
                    if (PasswordHelper.VerifyPassword(model.Password, history.OldPasswordSalt, history.OldPasswordHash))
                    {
                        ModelState.AddModelError("Password", "You cannot reuse one of your last 2 passwords.");
                        return View(model);
                    }
                }

                // Save old password to history
                _context.PasswordHistories.Add(new PasswordHistory
                {
                    MemberId = member.MemberId,
                    OldPasswordHash = member.PasswordHash,
                    OldPasswordSalt = member.PasswordSalt,
                    ChangedAt = DateTime.Now
                });

                // Update password
                string newSalt = PasswordHelper.GenerateSalt();
                string newHash = PasswordHelper.HashPassword(model.Password, newSalt);
                member.PasswordHash = newHash;
                member.PasswordSalt = newSalt;
                member.PasswordResetToken = null;
                member.PasswordResetTokenExpiry = null;
                member.LastPasswordChangeDate = DateTime.Now;

                await _context.SaveChangesAsync();
                await LogActivity(member.MemberId, "PasswordReset", "Password was reset via email link.");

                TempData["SuccessMessage"] = "Password reset successful. Please login.";
                return RedirectToAction("Login");
            }
            return View(model);
        }

        // ===================== HELPERS =====================

        private async Task LogActivity(int memberId, string activity, string details)
        {
            _context.AuditLogs.Add(new AuditLog
            {
                MemberId = memberId,
                Activity = activity,
                Details = details,
                Timestamp = DateTime.Now,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            });
            await _context.SaveChangesAsync();
        }

        private static string GenerateOTP()
        {
            return new Random().Next(100000, 999999).ToString();
        }
    }
}