using AS_Practical_Assignment.Data;
using AS_Practical_Assignment.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace AS_Practical_Assignment.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly EncryptionHelper _encryptionHelper;

        public HomeController(AppDbContext context, EncryptionHelper encryptionHelper)
        {
            _context = context;
            _encryptionHelper = encryptionHelper;
        }

        public async Task<IActionResult> Index()
        {
            string memberIdStr = HttpContext.Session.GetString("MemberId");
            if (memberIdStr == null)
                return RedirectToAction("Login", "Account");

            int memberId = int.Parse(memberIdStr);
            var member = await _context.Members.FindAsync(memberId);

            if (member == null)
            {
                HttpContext.Session.Clear();
                return RedirectToAction("Login", "Account");
            }

            // --- Multi-login detection ---
            string currentSessionToken = HttpContext.Session.GetString("SessionToken");
            if (member.SessionToken != currentSessionToken)
            {
                // Another device logged in — invalidate this session
                HttpContext.Session.Clear();
                TempData["ErrorMessage"] = "Your account has been logged in from another device.";
                return RedirectToAction("Login", "Account");
            }

            // --- Session timeout check (30 mins) ---
            string lastActivityStr = HttpContext.Session.GetString("LastActivity");
            if (lastActivityStr != null)
            {
                var lastActivity = System.DateTime.Parse(lastActivityStr);
                if ((System.DateTime.Now - lastActivity).TotalMinutes > 1)  // Keep at 1 for testing
                {
                    HttpContext.Session.Clear();
                    TempData["ErrorMessage"] = "Your session has expired due to inactivity. Please login again.";
                    return RedirectToAction("Login", "Account");
                }
            }
            HttpContext.Session.SetString("LastActivity", System.DateTime.Now.ToString());

            // --- Decrypt NRIC for display ---
            string decryptedNric = _encryptionHelper.Decrypt(member.NricEncrypted);

            // --- Decode WhoAmI back from HTML encoded ---
            string decodedWhoAmI = System.Net.WebUtility.HtmlDecode(member.WhoAmI ?? "");

            // --- Pass data to view via ViewData ---
            ViewData["FirstName"] = member.FirstName;
            ViewData["LastName"] = member.LastName;
            ViewData["Gender"] = member.Gender;
            ViewData["NRIC"] = decryptedNric;
            ViewData["Email"] = member.Email;
            ViewData["DateOfBirth"] = member.DateOfBirth.ToString("dd/MM/yyyy");
            ViewData["ResumePath"] = member.ResumePath;
            ViewData["WhoAmI"] = decodedWhoAmI;

            // --- Max password age check ---
            if (member.LastPasswordChangeDate.HasValue)
            {
                var daysSinceChange = (System.DateTime.Now - member.LastPasswordChangeDate.Value).TotalDays;
                if (daysSinceChange > 90)
                {
                    ViewData["PasswordExpiredWarning"] = true;
                }
            }

            return View();
        }

        // --- Custom Error Pages ---
        public IActionResult StatusCodeError(int code)
        {
            if (code == 404)
            {
                Response.StatusCode = 404;
                return View("NotFound");
            }
            else if (code == 403)
            {
                Response.StatusCode = 403;
                return View("Forbidden");
            }
            else
            {
                Response.StatusCode = 500;
                return View("Error");
            }
        }


        

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}