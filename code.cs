APPENDIX A: SOURCE CODE

This appendix presents the core source code of the TaskHub application, developed using ASP.NET Core 8 MVC and C#. The listings below show the principal controllers and a representative model class. Inline developer comments have been removed for brevity; the code is otherwise reproduced as implemented in the deployed system.

A.1 Account Controller (Authentication & Registration)

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.Tasks;
using TaskHub.Data;
using TaskHub.Models;
using TaskHub.Models.ViewModels;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace TaskHub.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly IEmailSender _emailSender;

        public AccountController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext context, IEmailSender emailSender)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _emailSender = emailSender;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View("~/Views/Home/Login.cshtml");
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, isPersistent: false, lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    return RedirectToAction("Dashboard", "Home");
                }

                ModelState.AddModelError(string.Empty, "Invalid email or password.");
            }

            return View("~/Views/Home/Login.cshtml", model);
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View("~/Views/Home/Register.cshtml");
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var newUser = new TaskHub.Models.Workspace
                {
                    Name = model.FullName + "'s Workspace",
                    Description = "Personal Workspace"
                };

                _context.Workspaces.Add(newWorkspace);
                await _context.SaveChangesAsync();

                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FullName = model.FullName,
                    Department = model.Department ?? "",
                    Organization = model.Organization ?? "",
                    ProfilePicture = "",
                    WorkspaceId = newWorkspace.Id.ToString()
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    await _signInManager.SignInAsync(user, isPersistent: false);

                    return RedirectToAction("Dashboard", "Home");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View("~/Views/Home/Register.cshtml", model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword(string Email)
        {
            if (string.IsNullOrEmpty(Email)) return View();

            var user = await _userManager.FindByEmailAsync(Email);

            if (user != null)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);

                var callbackUrl = Url.Action(
                    "ResetPassword",
                    "Account",
                    new { email = user.Email, token = token },
                    protocol: Request.Scheme);

                string subject = "Reset your TaskHub Password";

                string message = $"<div style=\"font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 8px;\">" +
                    $"<h2 style=\"color: #4f46e5;\">Password Reset</h2>" +
                    $"<p>Hello,</p>" +
                    $"<p>We received a request to reset your TaskHub account password. Click the button below to choose a new password:</p>" +
                    $"<div style=\"text-align: center; margin: 30px 0;\">" +
                    $"<a href=\"{callbackUrl}\" style=\"background-color: #4f46e5; color: white; padding: 14px 28px; text-decoration: none; border-radius: 8px; font-weight: bold; font-size: 16px; display: inline-block;\">Reset Password</a>" +
                    $"</div>" +
                    $"<p>If the button doesn't show up or doesn't work, copy and paste this link directly in your browser:</p>" +
                    $"<p style=\"word-break: break-all;\"><a href=\"{callbackUrl}\" style=\"color: #4A90E2;\">{callbackUrl}</a></p>" +
                    $"<hr style=\"border: none; border-top: 1px solid #eee; margin-top: 30px;\" />" +
                    $"<p style=\"font-size: 12px; color: #888;\">If you didn't request this, you can safely ignore this email.</p>" +
                    $"</div>";

                await _emailSender.SendEmailAsync(Email, subject, message);

                TempData["Message"] = "✔ EMAIL BHEJ DI GAYI HAI! Apna Inbox check karein.";
            }
            else
            {
                TempData["Message"] = "✗ ERROR: Yeh Email hamare system mein register hi nahi hai.";
            }

            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(string token, string email)
        {
            if (token == null || email == null)
            {
                return BadRequest("Invalid Password Reset Link.");
            }

            ViewData["Token"] = token;
            ViewData["Email"] = email;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword(string Email, string Token, string NewPassword)
        {
            var user = await _userManager.FindByEmailAsync(Email);
            if (user == null)
            {
                var result = await _userManager.ResetPasswordAsync(user, Token, NewPassword);
                if (result.Succeeded)
                {
                    TempData["SuccessMessage"] = "Password has been reset successfully! You can now login.";
                    return RedirectToAction("Login", "Account");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            ViewData["Token"] = Token;
            ViewData["Email"] = Email;
            return View();
        }
    }
}

A.2 Projects Controller (Project & Task Management)

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TaskHub.Data;
using TaskHub.Models;

namespace TaskHub.Controllers
{
    [Authorize]
    public class ProjectsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProjectsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User);

            var projects = await _context.Projects
                .Include(p => p.Tasks)
                .Include(p => p.Members)
                .Where(p => p.CreatedById == userId || p.Members.Any(m => m.UserId == userId))
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(projects);
        }

        [Authorize]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var userId = _userManager.GetUserId(User);
            var currentUser = await _userManager.GetUserAsync(User);

            var project = await _context.Projects
                .Include(p => p.Members).ThenInclude(m => m.User)
                .Include(p => p.Tasks).ThenInclude(t => t.Assignees).ThenInclude(ta => ta.User)
                .Include(p => p.Tasks).ThenInclude(t => t.Owner)
                .Include(p => p.Tasks).ThenInclude(t => t.Comments)
                .Include(p => p.Files).ThenInclude(f => f.UploadedBy)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (project == null) return NotFound();

            bool isOwner = project.CreatedById == userId;
            bool isMember = project.Members != null && project.Members.Any(m => m.UserId == userId);

            if (!isOwner && !isMember)
            {
                TempData["ErrorMessage"] = "You don't have access to this project.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.WorkspaceUsers = await _userManager.Users
                .Where(u => u.WorkspaceId == currentUser.WorkspaceId)
                .ToListAsync();

            return View(project);
        }

        public async Task<IActionResult> Tasks()
        {
            var userId = _userManager.GetUserId(User);
            var currentUser = await _userManager.GetUserAsync(User);

            if (string.IsNullOrEmpty(currentUser.WorkspaceId))
            {
                currentUser.WorkspaceId = Guid.NewGuid().ToString();
                await _userManager.UpdateAsync(currentUser);
            }

            ViewBag.Projects = await _context.Projects
                .Where(p => p.CreatedById == userId || p.Members.Any(m => m.UserId == userId))
                .ToListAsync();

            var tasks = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.Assignees).ThenInclude(ta => ta.User)
                .Where(t => t.Project.CreatedById == userId || t.OwnerId == userId || t.Assignees.Any(ta => ta.UserId == userId))
                .OrderBy(t => t.DueDate)
                .ToListAsync();

            ViewBag.WorkspaceUsers = await _userManager.Users
                .Where(u => u.WorkspaceId == currentUser.WorkspaceId)
                .ToListAsync();

            return View(tasks);
        }

        [HttpPost]
        public async Task<IActionResult> Create(string Name, string Description, DateTime DueDate, string Status, string Priority, IFormFile projectFile)
        {
            if (!string.IsNullOrEmpty(Name))
            {
                var project = new Project
                {
                    Name = Name,
                    Description = Description,
                    DueDate = DueDate == default ? DateTime.Now.AddDays(7) : DueDate,
                    Status = string.IsNullOrEmpty(Status) ? "Active" : Status,
                    Priority = string.IsNullOrEmpty(Priority) ? "Normal" : Priority,
                    CreatedById = _userManager.GetUserId(User),
                    CreatedAt = DateTime.Now
                };

                _context.Projects.Add(project);
                await _context.SaveChangesAsync();

                if (projectFile != null && projectFile.Length > 0)
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + projectFile.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await projectFile.CopyToAsync(stream);
                    }

                    string baseFileName = Path.GetFileNameWithoutExtension(projectFile.FileName);
                    var existingFiles = await _context.ProjectFiles
                        .Where(f => f.ProjectId == project.Id && f.FileName.Contains(baseFileName))
                        .ToListAsync();
                    int newVersion = existingFiles.Any() ? existingFiles.Max(f => f.Version) + 1 : 1;

                    var pFile = new ProjectFile
                    {
                        FileName = projectFile.FileName,
                        FilePath = "/uploads/" + uniqueFileName,
                        ContentType = projectFile.ContentType,
                        FileSize = (projectFile.Length / 1024).ToString() + " KB",
                        ProjectId = project.Id,
                        UploadedById = _userManager.GetUserId(User),
                        UploadedAt = DateTime.Now,
                        Version = newVersion
                    };

                    _context.ProjectFiles.Add(pFile);
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Index));
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateTask(int? ProjectId, string Title, string Description, string Priority, DateTime? DueDate, List<string> Assignees, IFormFile taskAttachment)
        {
            if (!string.IsNullOrEmpty(Title))
            {
                var task = new TaskItem
                {
                    Title = Title,
                    Description = Description,
                    ProjectId = ProjectId,
                    Priority = Priority ?? "Normal",
                    Status = "ToDo",
                    DueDate = DueDate,
                    OwnerId = _userManager.GetUserId(User),
                    CreatedAt = DateTime.Now,
                    Assignees = new List<TaskAssignee>()
                };

                if (Assignees != null && Assignees.Any())
                {
                    var currentUserId = _userManager.GetUserId(User);
                    foreach (var ald in Assignees)
                    {
                        string finalId = (ald == "me") ? currentUserId : ald;

                        if (!task.Assignees.Any(a => a.UserId == finalId))
                        {
                            task.Assignees.Add(new TaskAssignee { UserId = finalId });
                        }
                    }
                }

                _context.Tasks.Add(task);
                await _context.SaveChangesAsync();

                if (taskAttachment != null && taskAttachment.Length > 0)
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + taskAttachment.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await taskAttachment.CopyToAsync(stream);
                    }

                    string baseFileName = Path.GetFileNameWithoutExtension(taskAttachment.FileName);
                    var existingFiles = await _context.ProjectFiles
                        .Where(f => f.TaskItemId == task.Id && f.FileName.Contains(baseFileName))
                        .ToListAsync();
                    int newVersion = existingFiles.Any() ? existingFiles.Max(f => f.Version) + 1 : 1;

                    var projectFile = new ProjectFile
                    {
                        FileName = taskAttachment.FileName,
                        FilePath = "/uploads/" + uniqueFileName,
                        ContentType = taskAttachment.ContentType,
                        FileSize = (taskAttachment.Length / 1024).ToString() + " KB",
                        ProjectId = ProjectId,
                        TaskItemId = task.Id,
                        UploadedById = _userManager.GetUserId(User),
                        UploadedAt = DateTime.Now,
                        Version = newVersion
                    };

                    _context.ProjectFiles.Add(projectFile);
                    await _context.SaveChangesAsync();
                }

                if (ProjectId.HasValue && ProjectId.Value > 0)
                {
                    return RedirectToAction(nameof(Details), new { id = ProjectId.Value });
                }

                return RedirectToAction(nameof(Tasks));
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UploadFile(int ProjectId, IFormFile uploadedFile)
        {
            if (uploadedFile != null && uploadedFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = Guid.NewGuid().ToString() + "_" + uploadedFile.FileName;
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await uploadedFile.CopyToAsync(stream);
                }

                string baseFileName = Path.GetFileNameWithoutExtension(uploadedFile.FileName);
                var existingFiles = await _context.ProjectFiles
                    .Where(f => f.ProjectId == ProjectId && f.TaskItemId == null && f.FileName.Contains(baseFileName))
                    .ToListAsync();
                int newVersion = existingFiles.Any() ? existingFiles.Max(f => f.Version) + 1 : 1;

                var projectFile = new ProjectFile
                {
                    FileName = uploadedFile.FileName,
                    FilePath = "/uploads/" + uniqueFileName,
                    ContentType = uploadedFile.ContentType,
                    FileSize = (uploadedFile.Length / 1024).ToString() + " KB",
                    ProjectId = ProjectId,
                    TaskItemId = null,
                    UploadedById = _userManager.GetUserId(User),
                    UploadedAt = DateTime.Now,
                    Version = newVersion
                };

                _context.ProjectFiles.Add(projectFile);
                await _context.SaveChangesAsync();

                return Ok();
            }

            return BadRequest();
        }

        [HttpPost]
        public async Task<IActionResult> UpdateTaskStatus(int id, string status)
        {
            var task = await _context.Tasks.FindAsync(id);
            if (task != null)
            {
                task.Status = status;
                await _context.SaveChangesAsync();
            }

            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> AddComment(int taskid, string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                var comment = new TaskComment
                {
                    TaskItemId = taskid,
                    Text = text,
                    UserId = _userManager.GetUserId(User),
                    CreatedAt = DateTime.Now
                };

                _context.TaskComments.Add(comment);
                await _context.SaveChangesAsync();
            }

            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> UploadTaskAttachment(int taskid, IFormFile file)
        {
            if (file != null && file.Length > 0)
            {
                var task = await _context.Tasks.FindAsync(taskid);
                if (task == null) return NotFound();

                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                string baseFileName = Path.GetFileNameWithoutExtension(file.FileName);
                var existingFiles = await _context.ProjectFiles
                    .Where(f => f.TaskItemId == taskid && f.FileName.Contains(baseFileName))
                    .ToListAsync();
                int newVersion = existingFiles.Any() ? existingFiles.Max(f => f.Version) + 1 : 1;

                var projectFile = new ProjectFile
                {
                    FileName = file.FileName,
                    FilePath = "/uploads/" + uniqueFileName,
                    ContentType = file.ContentType,
                    FileSize = (file.Length / 1024).ToString() + " KB",
                    ProjectId = task.ProjectId,
                    TaskItemId = taskid,
                    UploadedById = _userManager.GetUserId(User),
                    UploadedAt = DateTime.Now,
                    Version = newVersion
                };

                _context.ProjectFiles.Add(projectFile);
                await _context.SaveChangesAsync();
                return Ok();
            }

            return BadRequest();
        }

        [HttpPost]
        public async Task<IActionResult> UpdateTaskProgress(int id, int progress)
        {
            var task = await _context.Tasks.FindAsync(id);
            if (task != null)
            {
                task.Progress = progress;
                await _context.SaveChangesAsync();
            }

            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> GetTaskDetails(int id)
        {
            var task = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.Assignees).ThenInclude(ta => ta.User)
                .Include(t => t.Owner)
                .Include(t => t.Attachments).ThenInclude(a => a.UploadedBy)
                .Include(t => t.Comments).ThenInclude(c => c.User)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null) return NotFound();

            return PartialView("_TaskDetailPanel", task);
        }

        [HttpGet]
        public IActionResult ViewFile(int fileId)
        {
            var file = _context.ProjectFiles.Find(fileId);
            if (file == null) return NotFound();

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", file.FilePath.TrimStart('/'));
            if (!System.IO.File.Exists(filePath)) return NotFound();

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            string mimeType = "application/octet-stream";

            if (ext == ".pdf") mimeType = "application/pdf";
            else if (ext == ".jpg" || ext == ".jpeg") mimeType = "image/jpeg";
            else if (ext == ".png") mimeType = "image/png";
            else if (ext == ".txt") mimeType = "text/plain";

            return PhysicalFile(filePath, mimeType);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProjectStatus(int id, string status)
        {
            var project = await _context.Projects.FindAsync(id);
            if (project != null)
            {
                project.Status = status;
                await _context.SaveChangesAsync();
            }

            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> AddMember(int ProjectId, string UserId)
        {
            bool alreadyExists = await _context.ProjectMembers.AnyAsync(m => m.ProjectId == ProjectId && m.UserId == UserId);

            if (!alreadyExists && !string.IsNullOrEmpty(UserId))
            {
                var newMember = new ProjectMember
                {
                    ProjectId = ProjectId,
                    UserId = UserId,
                    AddedAt = DateTime.Now
                };

                _context.ProjectMembers.Add(newMember);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Details), new { id = ProjectId });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProject(int id)
        {
            var userId = _userManager.GetUserId(User);

            var project = await _context.Projects
                .Include(p => p.Tasks).ThenInclude(t => t.Comments)
                .Include(p => p.Tasks).ThenInclude(t => t.Attachments)
                .Include(p => p.Files)
                .Include(p => p.Members)
                .FirstOrDefaultAsync(p => p.Id == id && p.CreatedById == userId);

            if (project != null)
            {
                try
                {
                    _context.ProjectFiles.RemoveRange(project.Files);
                    _context.Tasks.RemoveRange(project.Tasks);
                    _context.Projects.Remove(project);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Project and all its tasks have been deleted successfully.";
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = "System Error: " + ex.Message;
                }
            }
            else
            {
                TempData["ErrorMessage"] = "You do not have permission to delete this project.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> SendWhatsAppReminder(int id)
        {
            var task = await _context.Tasks
                .Include(t => t.Project)
                .Include(t => t.Assignees).ThenInclude(ta => ta.User)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null) return Json(new { success = false, message = "Task not found." });

            var assigneesWithWa = task.Assignees
                .Where(a => !string.IsNullOrEmpty(a.User.WhatsAppNumber))
                .ToList();

            if (assigneesWithWa.Any())
            {
                var waLinks = new List<object>();

                foreach (var assignee in assigneesWithWa)
                {
                    var userToRemind = assignee.User;
                    string number = userToRemind.WhatsAppNumber.Replace("+", "").Replace(" ", "").Replace("-", "");

                    if (number.StartsWith("0"))
                    {
                        number = "92" + number.Substring(1);
                    }

                    string msg = $"*TaskHub Reminder* ⏰\nHi {userToRemind.FullName},\nThis is a gentle reminder for your assigned task:\n\n*Task:* {task.Title}\n*Project:* {(task.Project != null ? task.Project.Name : "Standalone")}\n*Priority:* {task.Priority}\n*Due Date:* {(task.DueDate.HasValue ? task.DueDate.Value.ToString("dd MMM yyyy") : "No Due Date")}\n*Status:* {task.Status}\n\nPlease update the progress on your dashboard.\n\n_Automated by TaskHub System_";

                    waLinks.Add(new
                    {
                        name = userToRemind.FullName,
                        url = $"https://wa.me/{number}?text={Uri.EscapeDataString(msg)}"
                    });
                }

                return Json(new { success = true, links = waLinks });
            }

            return Json(new { success = false, message = "Kisi bhi assignee ne apna WhatsApp number profile mein add nahi kiya hai!" });
        }
    }
}