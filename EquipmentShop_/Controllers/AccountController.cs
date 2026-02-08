using EquipmentShop.Core.Constants;
using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Enums;
using EquipmentShop.Core.Helpers;
using EquipmentShop.Core.Interfaces;
using EquipmentShop.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace EquipmentShop.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AccountController> _logger;
        private readonly IEmailService _emailService;
        private readonly IOrderRepository _orderRepository;
        private readonly IFileStorageService _fileStorageService;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<AccountController> logger,
            IEmailService emailService,
            IOrderRepository orderRepository,
            IFileStorageService fileStorageService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _emailService = emailService;
            _orderRepository = orderRepository;
            _fileStorageService = fileStorageService;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            var pendingAddToCart = HttpContext.Session.GetString("PendingAddToCart");
            if (!string.IsNullOrEmpty(pendingAddToCart))
            {
                ViewData["PendingAction"] = "У вас есть товар, ожидающий добавления в корзину";
            }
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null)
                {
                    ModelState.AddModelError("", "Неверный email или пароль");
                    return View(model);
                }

                var result = await _signInManager.PasswordSignInAsync(user.UserName, model.Password, model.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    _logger.LogInformation("Пользователь {Email} вошел в систему", model.Email);
                    user.LastLoginAt = DateTime.UtcNow;
                    await _userManager.UpdateAsync(user);

                    var pendingAddToCart = HttpContext.Session.GetString("PendingAddToCart");
                    if (!string.IsNullOrEmpty(pendingAddToCart))
                    {
                        try
                        {
                            var parts = pendingAddToCart.Split(',');
                            if (parts.Length == 2 &&
                                int.TryParse(parts[0], out int productId) &&
                                int.TryParse(parts[1], out int quantity))
                            {
                                var cartService = HttpContext.RequestServices.GetRequiredService<IShoppingCartService>();
                                var cart = await cartService.GetUserCartAsync(user.Id);
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

                    return RedirectToLocal(returnUrl);
                }

                if (result.IsLockedOut)
                {
                    _logger.LogWarning("Аккаунт пользователя {Email} заблокирован", model.Email);
                    ModelState.AddModelError("", "Аккаунт заблокирован. Попробуйте позже.");
                }
                else
                {
                    ModelState.AddModelError("", "Неверный email или пароль");
                    _logger.LogWarning("Неудачная попытка входа для пользователя {Email}", model.Email);
                }
            }
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            if (ModelState.IsValid)
            {
                var existingUser = await _userManager.FindByEmailAsync(model.Email);
                if (existingUser != null)
                {
                    ModelState.AddModelError("", "Пользователь с таким email уже зарегистрирован.");
                    return View(model);
                }

                var user = new ApplicationUser
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

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    _logger.LogInformation("Создан новый пользователь: {Email}", model.Email);
                    await _userManager.AddToRoleAsync(user, AppConstants.CustomerRole);

                    try
                    {
                        await _emailService.SendWelcomeEmailAsync(user.Email, user.FullName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка отправки приветственного письма для {Email}", model.Email);
                    }

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    TempData["Success"] = $"Регистрация прошла успешно! Добро пожаловать, {user.FirstName}!";
                    return RedirectToLocal(returnUrl);
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                    _logger.LogWarning("Ошибка регистрации {Email}: {Error}", model.Email, error.Description);
                }
            }
            return View(model);
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> OrderDetails(string orderNumber)
        {
            if (string.IsNullOrWhiteSpace(orderNumber)) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var order = await _orderRepository.GetByOrderNumberAsync(orderNumber);

            if (order == null || order.UserId != user.Id) return NotFound();

            var viewModel = new UserOrderDetailsViewModel
            {
                OrderNumber = order.OrderNumber,
                OrderDate = order.OrderDate,
                Status = order.Status,
                Total = order.Total,
                // Адрес доставки
                ShippingAddress = order.ShippingAddress,
                ShippingCity = order.ShippingCity,
                ShippingRegion = order.ShippingRegion,
                ShippingPostalCode = order.ShippingPostalCode,
                ShippingCountry = order.ShippingCountry,
                Items = order.OrderItems.Select(oi => new UserOrderItemViewModel
                {
                    ProductName = oi.ProductName,
                    Quantity = oi.Quantity,
                    Price = oi.UnitPrice,
                    Total = oi.TotalPrice
                }).ToList()
            };

            return View(viewModel);
        }

        // === Внутренние ViewModel, специфичные для AccountController ===
        public class UserOrderDetailsViewModel
        {
            public string OrderNumber { get; set; } = string.Empty;
            public DateTime OrderDate { get; set; }
            public OrderStatus Status { get; set; }
            public decimal Total { get; set; }

            // Адрес доставки
            public string ShippingAddress { get; set; } = string.Empty;
            public string? ShippingCity { get; set; }
            public string? ShippingRegion { get; set; }
            public string? ShippingPostalCode { get; set; }
            public string? ShippingCountry { get; set; }

            public List<UserOrderItemViewModel> Items { get; set; } = new();
        }

        public class UserOrderItemViewModel
        {
            public string ProductName { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public decimal Price { get; set; }
            public decimal Total { get; set; }
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelOrder(string orderNumber)
        {
            if (string.IsNullOrWhiteSpace(orderNumber)) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            var order = await _orderRepository.GetByOrderNumberAsync(orderNumber);
            if (order == null || order.UserId != user.Id) return NotFound();

            var isAllowedStatus = order.Status == OrderStatus.Pending || order.Status == OrderStatus.Processing;
            var isWithinTimeWindow = order.OrderDate >= DateTime.UtcNow.AddMinutes(-30);
            if (!isAllowedStatus || !isWithinTimeWindow)
            {
                TempData["Error"] = "Невозможно отменить заказ: прошло более 30 минут или заказ уже обрабатывается.";
                return RedirectToAction("OrderDetails", new { orderNumber });
            }

            var orderService = HttpContext.RequestServices.GetRequiredService<IOrderService>();
            await orderService.CancelOrderAsync(order.Id, "Отменено пользователем");
            TempData["Success"] = $"Заказ #{orderNumber} успешно отменён.";
            return RedirectToAction("OrderDetails", new { orderNumber });
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Orders()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var orders = await _orderRepository.GetByUserIdAsync(user.Id);
            var viewModel = orders.Select(order => new UserOrderViewModel
            {
                OrderNumber = order.OrderNumber,
                OrderDate = order.OrderDate,
                Total = order.Total,
                Status = order.Status,
                ItemCount = order.OrderItems.Count
            }).ToList();
            return View(viewModel);
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

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> EditProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var model = new UserProfileViewModel
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Phone = user.PhoneNumber ?? string.Empty,
                AvatarUrl = user.AvatarUrl
            };
            return View(model);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(UserProfileViewModel model, IFormFile? avatarFile)
        {
            if (!ModelState.IsValid)
            {
                return View("EditProfile", model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.PhoneNumber = model.Phone;

            if (avatarFile != null && avatarFile.Length > 0)
            {
                if (avatarFile.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError("avatarFile", "Файл слишком большой (макс. 5 МБ)");
                    return View("EditProfile", model);
                }

                var ext = Path.GetExtension(avatarFile.FileName).ToLowerInvariant();
                if (!AppConstants.AllowedImageExtensions.Contains(ext))
                {
                    ModelState.AddModelError("avatarFile", "Недопустимый формат файла");
                    return View("EditProfile", model);
                }

                if (!string.IsNullOrEmpty(user.AvatarUrl) &&
                    !user.AvatarUrl.Equals(AppConstants.DefaultUserAvatar, StringComparison.OrdinalIgnoreCase))
                {
                    await _fileStorageService.DeleteFileAsync(user.AvatarUrl);
                }

                var fileName = await _fileStorageService.GenerateUniqueFileName(avatarFile.FileName);
                var filePath = await _fileStorageService.SaveUserAvatarAsync(avatarFile.OpenReadStream(), fileName);
                user.AvatarUrl = filePath;
            }

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["Success"] = "Профиль успешно обновлён";
                return RedirectToAction("Profile");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View("EditProfile", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                _logger.LogInformation("Пользователь {Email} вышел из системы", user.Email);
            }
            await _signInManager.SignOutAsync();
            HttpContext.Session.Remove("CartId");
            TempData["Success"] = "Вы успешно вышли из системы";
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var orderCount = await _orderRepository.GetByUserIdAsync(user.Id);
            var totalOrders = orderCount.Count();

            var model = new UserProfileViewModel
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Phone = user.PhoneNumber,
                AvatarUrl = user.AvatarUrl,
                RegisteredAt = user.RegisteredAt,
                OrderCount = totalOrders,
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
                }).ToList() ?? new List<AddressViewModel>()
            };
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user != null && await _userManager.IsEmailConfirmedAsync(user))
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    var callbackUrl = Url.Action("ResetPassword", "Account",
                        new { userId = user.Id, token = token }, protocol: HttpContext.Request.Scheme);
                    try
                    {
                        await _emailService.SendPasswordResetAsync(user.Email, callbackUrl);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при отправке письма для сброса пароля");
                    }
                }
                TempData["Success"] = "Если аккаунт с таким email существует, на него было отправлено письмо для сброса пароля";
                return RedirectToAction("Login");
            }
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(string token, string userId)
        {
            if (token == null || userId == null)
            {
                return RedirectToAction("Index", "Home");
            }
            var model = new ResetPasswordViewModel
            {
                Token = token,
                UserId = userId
            };
            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByIdAsync(model.UserId);
                if (user != null)
                {
                    var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
                    if (result.Succeeded)
                    {
                        TempData["Success"] = "Пароль успешно сброшен";
                        return RedirectToAction("Login");
                    }
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                }
                else
                {
                    ModelState.AddModelError("", "Пользователь не найден");
                }
            }
            return View(model);
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> ChangePassword()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }
            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return NotFound();
                }
                var result = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);
                if (result.Succeeded)
                {
                    await _signInManager.RefreshSignInAsync(user);
                    TempData["Success"] = "Пароль успешно изменен";
                    return RedirectToAction("Profile");
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }
            return View(model);
        }

        [HttpGet]
        public IActionResult AccessDenied(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }
    }
}