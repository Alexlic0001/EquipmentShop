using EquipmentShop.Core.Constants;
using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Enums;
using EquipmentShop.Core.Helpers;
using EquipmentShop.Core.Interfaces;
using EquipmentShop.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Claims;

namespace EquipmentShop_.Controllers
{
    public class AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ILogger<AccountController> logger,
        IEmailService emailService,
        IOrderRepository orderRepository,
        IFileStorageService fileStorageService) : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager = signInManager;
        private readonly UserManager<ApplicationUser> _userManager = userManager;
        private readonly ILogger<AccountController> _logger = logger;
        private readonly IEmailService _emailService = emailService;
        private readonly IOrderRepository _orderRepository = orderRepository;
        private readonly IFileStorageService _fileStorageService = fileStorageService;

        [HttpGet, AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            SetReturnUrl(returnUrl);
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("PendingAddToCart")))
                ViewData["PendingAction"] = "У вас есть товар, ожидающий добавления в корзину";
            return View();
        }

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            SetReturnUrl(returnUrl);
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                AddModelError("Неверный email или пароль");
                _logger.LogWarning("Неудачная попытка входа: пользователь не найден для {Email}", model.Email);
                return View(model);
            }

            Microsoft.AspNetCore.Identity.SignInResult result = await _signInManager.PasswordSignInAsync(user.UserName, model.Password, model.RememberMe, lockoutOnFailure: false);
            if (result.Succeeded)
            {
                await UpdateLastLoginAsync(user);
                await ProcessPendingCartAsync(user.Id);
                _logger.LogInformation("Пользователь {Email} вошел в систему", model.Email);
                return RedirectToLocal(returnUrl);
            }

            HandleSignInResult(result, model.Email);
            return View(model);
        }

        [HttpGet, AllowAnonymous]
        public IActionResult Register(string? returnUrl = null)
        {
            SetReturnUrl(returnUrl);
            return View();
        }

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
        {
            SetReturnUrl(returnUrl);
            if (!ModelState.IsValid) return View(model);

            if (await _userManager.FindByEmailAsync(model.Email) != null)
            {
                ModelState.AddModelError("", "Пользователь с таким email уже зарегистрирован.");
                return View(model);
            }

            var user = CreateApplicationUser(model);
            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                LogAndAddErrors(result.Errors, model.Email);
                return View(model);
            }

            await FinalizeRegistrationAsync(user, model.FirstName);
            TempData["Success"] = $"Регистрация прошла успешно! Добро пожаловать, {user.FirstName}!";
            return RedirectToLocal(returnUrl);
        }

        [HttpGet, Authorize]
        public async Task<IActionResult> OrderDetails(string orderNumber)
        {
            if (string.IsNullOrWhiteSpace(orderNumber)) return NotFound();
            var (user, order) = await GetOrderWithValidationAsync(orderNumber);
            if (order == null || order.UserId != user.Id) return NotFound();

            return View(new UserOrderDetailsViewModel
            {
                OrderNumber = order.OrderNumber,
                OrderDate = order.OrderDate,
                Status = order.Status,
                Total = order.Total,
                ShippingAddress = order.ShippingAddress,
                ShippingCity = order.ShippingCity,
                ShippingRegion = order.ShippingRegion,
                ShippingPostalCode = order.ShippingPostalCode,
                ShippingCountry = order.ShippingCountry,
                Items = [.. order.OrderItems.Select(oi => new UserOrderItemViewModel
                {
                    ProductName = oi.ProductName,
                    Quantity = oi.Quantity,
                    Price = oi.UnitPrice,
                    Total = oi.TotalPrice
                })]
            });
        }

        [HttpPost, Authorize, ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(string orderNumber)
        {
            if (string.IsNullOrWhiteSpace(orderNumber)) return NotFound();
            var (user, order) = await GetOrderWithValidationAsync(orderNumber);
            if (order == null || order.UserId != user.Id) return NotFound();

            if (!CanCancelOrder(order))
            {
                TempData["Error"] = "Невозможно отменить заказ: прошло более 30 минут или заказ уже обрабатывается.";
                return RedirectToAction("OrderDetails", new { orderNumber });
            }

            var orderService = HttpContext.RequestServices.GetRequiredService<IOrderService>();
            await orderService.CancelOrderAsync(order.Id, "Отменено пользователем");
            TempData["Success"] = $"Заказ #{orderNumber} успешно отменён.";
            return RedirectToAction("OrderDetails", new { orderNumber });
        }

        [HttpGet, Authorize]
        public async Task<IActionResult> Orders()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var orders = await _orderRepository.GetByUserIdAsync(user.Id);
            return View(orders.Select(o => new UserOrderViewModel
            {
                OrderNumber = o.OrderNumber,
                OrderDate = o.OrderDate,
                Total = o.Total,
                Status = o.Status,
                ItemCount = o.OrderItems.Count
            }).ToList());
        }

        [HttpGet, Authorize]
        public async Task<IActionResult> EditProfile() =>
            View(await BuildUserProfileViewModelAsync());

        [HttpPost, Authorize, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(UserProfileViewModel model, IFormFile? avatarFile)
        {
            if (!ModelState.IsValid) return View("EditProfile", model);

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            UpdateUserFromModel(user, model);
            if (avatarFile?.Length > 0 && !await ProcessAvatarUploadAsync(user, avatarFile, model))
                return View("EditProfile", model);

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["Success"] = "Профиль успешно обновлён";
                return RedirectToAction("Profile");
            }

            LogAndAddErrors(result.Errors);
            return View("EditProfile", model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
                _logger.LogInformation("Пользователь {Email} вышел из системы", user.Email);

            await _signInManager.SignOutAsync();
            HttpContext.Session.Remove("CartId");
            TempData["Success"] = "Вы успешно вышли из системы";
            return RedirectToAction("Index", "Home");
        }

        [HttpGet, Authorize]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var orders = await _orderRepository.GetByUserIdAsync(user.Id);
            return View(new UserProfileViewModel
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Phone = user.PhoneNumber,
                AvatarUrl = user.AvatarUrl,
                RegisteredAt = user.RegisteredAt,
                OrderCount = orders.Count(),
                Addresses = user.AdditionalAddresses?.Select(a => new AddressViewModel
                {
                    Title = a.Title,
                    AddressLine1 = a.AddressLine1,
                    AddressLine2 = a.AddressLine2,
                    City = a.City,
                    Region = a.Region,
                    PostalCode = a.PostalCode,
                    Country = a.Country,
                    IsDefault = a.IsDefault
                }).ToList() ?? []
            });
        }

        [HttpGet, AllowAnonymous]
        public IActionResult ForgotPassword() => View();

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null && await _userManager.IsEmailConfirmedAsync(user))
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    var callbackUrl = Url.Action("ResetPassword", "Account",
                        new { userId = user.Id, token }, protocol: HttpContext.Request.Scheme);
                    await SendPasswordResetEmailAsync(user.Email, callbackUrl);
                }
                TempData["Success"] = "Если аккаунт с таким email существует, на него было отправлено письмо для сброса пароля";
                return RedirectToAction("Login");
            }
            return View(model);
        }

        [HttpGet, AllowAnonymous]
        public IActionResult ResetPassword(string token, string userId) =>
            string.IsNullOrEmpty(token) || string.IsNullOrEmpty(userId)
                ? RedirectToAction("Index", "Home")
                : View(new ResetPasswordViewModel { Token = token, UserId = userId });

        [HttpPost, AllowAnonymous, ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                ModelState.AddModelError("", "Пользователь не найден");
                return View(model);
            }

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
            if (result.Succeeded)
            {
                TempData["Success"] = "Пароль успешно сброшен";
                return RedirectToAction("Login");
            }

            LogAndAddErrors(result.Errors);
            return View(model);
        }

        [HttpGet, Authorize]
        public async Task<IActionResult> ChangePassword() =>
            await _userManager.GetUserAsync(User) == null ? NotFound() as IActionResult : View();

        [HttpPost, Authorize, ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var result = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);
            if (result.Succeeded)
            {
                await _signInManager.RefreshSignInAsync(user);
                TempData["Success"] = "Пароль успешно изменен";
                return RedirectToAction("Profile");
            }

            LogAndAddErrors(result.Errors);
            return View(model);
        }

        [HttpGet]
        public IActionResult AccessDenied(string? returnUrl = null)
        {
            SetReturnUrl(returnUrl);
            return View();
        }

        // === Вложенные ViewModel (без изменений) ===
        public class UserOrderDetailsViewModel
        {
            public string OrderNumber { get; set; } = string.Empty;
            public DateTime OrderDate { get; set; }
            public OrderStatus Status { get; set; }
            public decimal Total { get; set; }
            public string ShippingAddress { get; set; } = string.Empty;
            public string? ShippingCity { get; set; }
            public string? ShippingRegion { get; set; }
            public string? ShippingPostalCode { get; set; }
            public string? ShippingCountry { get; set; }
            public List<UserOrderItemViewModel> Items { get; set; } = [];
        }

        public class UserOrderItemViewModel
        {
            public string ProductName { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public decimal Price { get; set; }
            public decimal Total { get; set; }
        }

        public class UserOrderViewModel
        {
            public string OrderNumber { get; set; } = string.Empty;
            public DateTime OrderDate { get; set; }
            public decimal Total { get; set; }
            public OrderStatus Status { get; set; }
            public int ItemCount { get; set; }
            public string StatusDisplay => EnumHelper<OrderStatus>.GetDisplayName(Status);
        }

        // === Вспомогательные методы ===
        private void SetReturnUrl(string? returnUrl) => ViewData["ReturnUrl"] = returnUrl;

        private void AddModelError(string message) => ModelState.AddModelError("", message);

        private async Task ProcessPendingCartAsync(string userId)
        {
            var pending = HttpContext.Session.GetString("PendingAddToCart");
            if (string.IsNullOrEmpty(pending)) return;

            try
            {
                var parts = pending.Split(',');
                if (parts.Length == 2 &&
                    int.TryParse(parts[0], out var productId) &&
                    int.TryParse(parts[1], out var quantity))
                {
                    var cartService = HttpContext.RequestServices.GetRequiredService<IShoppingCartService>();
                    var cart = await cartService.GetUserCartAsync(userId);
                    await cartService.AddItemAsync(cart.Id, productId, quantity);
                    HttpContext.Session.Remove("PendingAddToCart");
                    TempData["Success"] = "Товар добавлен в вашу корзину";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении отложенного товара в корзину");
            }
        }

        private async Task UpdateLastLoginAsync(ApplicationUser user)
        {
            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
        }

        private void HandleSignInResult(Microsoft.AspNetCore.Identity.SignInResult result, string email)
        {
            if (result.IsLockedOut)
            {
                AddModelError("Аккаунт заблокирован. Попробуйте позже.");
                _logger.LogWarning("Аккаунт пользователя {Email} заблокирован", email);
            }
            else
            {
                AddModelError("Неверный email или пароль");
                _logger.LogWarning("Неудачная попытка входа для пользователя {Email}", email);
            }
        }

        private ApplicationUser CreateApplicationUser(RegisterViewModel model) => new()
        {
            UserName = model.Email,
            Email = model.Email,
            FirstName = model.FirstName,
            LastName = model.LastName,
            PhoneNumber = model.Phone,
            SubscribeToNewsletter = model.SubscribeToNewsletter,
            EmailNotifications = true,
            SmsNotifications = false,
            RegisteredAt = DateTime.UtcNow,
            EmailConfirmed = true
        };

        private async Task FinalizeRegistrationAsync(ApplicationUser user, string firstName)
        {
            ArgumentNullException.ThrowIfNull(firstName);
            await _userManager.AddToRoleAsync(user, AppConstants.CustomerRole);
            _ = SendWelcomeEmailAsync(user.Email, user.FullName); // Fire-and-forget
            await _signInManager.SignInAsync(user, isPersistent: false);
        }

        private async Task SendWelcomeEmailAsync(string email, string fullName)
        {
            try { await _emailService.SendWelcomeEmailAsync(email, fullName); }
            catch (Exception ex) { _logger.LogError(ex, "Ошибка отправки приветственного письма для {Email}", email); }
        }

        private async Task SendPasswordResetEmailAsync(string email, string callbackUrl)
        {
            try { await _emailService.SendPasswordResetAsync(email, callbackUrl); }
            catch (Exception ex) { _logger.LogError(ex, "Ошибка при отправке письма для сброса пароля"); }
        }

        private void LogAndAddErrors(IEnumerable<IdentityError> errors, string? email = null)
        {
            foreach (var error in errors)
            {
                ModelState.AddModelError("", error.Description);
                if (email != null)
                    _logger.LogWarning("Ошибка регистрации {Email}: {Error}", email, error.Description);
                else
                    _logger.LogWarning("Ошибка Identity: {Error}", error.Description);
            }
        }

        private bool CanCancelOrder(Order order)
        {
            return (order.Status == OrderStatus.Pending || order.Status == OrderStatus.Processing) &&
            order.OrderDate >= DateTime.UtcNow.AddMinutes(-30);
        }

        private async Task<(ApplicationUser? User, Order? Order)> GetOrderWithValidationAsync(string orderNumber)
        {
            var user = await _userManager.GetUserAsync(User);
            var order = await _orderRepository.GetByOrderNumberAsync(orderNumber);
            return (user, order);
        }

        private void UpdateUserFromModel(ApplicationUser user,
                                         UserProfileViewModel model)
        {
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.PhoneNumber = model.Phone;
        }

        private async Task<bool> ProcessAvatarUploadAsync(ApplicationUser user, IFormFile file, UserProfileViewModel model)
        {
            ArgumentNullException.ThrowIfNull(model);
            const int maxFileSize = 5 * 1024 * 1024;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (file.Length > maxFileSize)
            {
                ModelState.AddModelError("avatarFile", "Файл слишком большой (макс. 5 МБ)");
                return false;
            }

            if (!AppConstants.AllowedImageExtensions.Contains(ext))
            {
                ModelState.AddModelError("avatarFile", "Недопустимый формат файла");
                return false;
            }

            if (!string.IsNullOrEmpty(user.AvatarUrl) &&
                !user.AvatarUrl.Equals(AppConstants.DefaultUserAvatar, StringComparison.OrdinalIgnoreCase))
            {
                await _fileStorageService.DeleteFileAsync(user.AvatarUrl);
            }

            var fileName = await _fileStorageService.GenerateUniqueFileName(file.FileName);
            user.AvatarUrl = await _fileStorageService.SaveUserAvatarAsync(file.OpenReadStream(), fileName);
            return true;
        }

        private async Task<UserProfileViewModel> BuildUserProfileViewModelAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return null!;

            return new UserProfileViewModel
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Phone = user.PhoneNumber ?? string.Empty,
                AvatarUrl = user.AvatarUrl
            };
        }

        private IActionResult RedirectToLocal(string? returnUrl) =>
            Url.IsLocalUrl(returnUrl) ? Redirect(returnUrl) : RedirectToAction("Index", "Home");
    }
}