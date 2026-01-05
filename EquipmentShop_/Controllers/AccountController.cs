using EquipmentShop.Core.Entities;
using EquipmentShop.Core.Interfaces;
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

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<AccountController> logger,
            IEmailService emailService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _emailService = emailService;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            // Проверяем, есть ли сохраненные данные для входа
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
                // Ищем пользователя по email
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null)
                {
                    ModelState.AddModelError("", "Неверный email или пароль");
                    return View(model);
                }

                // Вход с использованием UserName (а не Email)
                var result = await _signInManager.PasswordSignInAsync(
                    user.UserName, // Используем UserName, а не Email
                    model.Password,
                    model.RememberMe,
                    lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Пользователь {Email} вошел в систему", model.Email);

                    // Обновляем время последнего входа
                    user.LastLoginAt = DateTime.UtcNow;
                    await _userManager.UpdateAsync(user);

                    // Проверяем, есть ли отложенное добавление в корзину
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
                                // Добавляем товар в корзину пользователя
                                var cartService = HttpContext.RequestServices.GetRequiredService<IShoppingCartService>();
                                var cart = await cartService.GetUserCartAsync(user.Id);
                                await cartService.AddItemAsync(cart.Id, productId, quantity);

                                // Очищаем отложенное действие
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
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    PhoneNumber = model.Phone,
                    SubscribeToNewsletter = model.SubscribeToNewsletter,
                    EmailNotifications = true,
                    RegisteredAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("Создан новый пользователь {Email}", model.Email);

                    // Добавляем пользователя в роль Customer
                    await _userManager.AddToRoleAsync(user, "Customer");

                    // Отправляем письмо приветствия
                    try
                    {
                        await _emailService.SendWelcomeEmailAsync(user.Email, user.FullName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при отправке приветственного письма");
                    }

                    // Автоматический вход после регистрации
                    await _signInManager.SignInAsync(user, isPersistent: false);

                    TempData["Success"] = "Регистрация прошла успешно! Добро пожаловать в EquipmentShop";

                    return RedirectToLocal(returnUrl);
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                    _logger.LogWarning("Ошибка регистрации пользователя {Email}: {Error}", model.Email, error.Description);
                }
            }

            return View(model);
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

            // Очищаем сессию корзины при выходе
            HttpContext.Session.Remove("CartId");

            TempData["Success"] = "Вы успешно вышли из системы";

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound();
            }

            var model = new UserProfileViewModel
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Phone = user.PhoneNumber,
                AvatarUrl = user.AvatarUrl,
                RegisteredAt = user.RegisteredAt,
                OrderCount = user.TotalOrders,
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

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(UserProfileViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return NotFound();
                }

                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.PhoneNumber = model.Phone;

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    TempData["Success"] = "Профиль успешно обновлен";
                    return RedirectToAction("Profile");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }

            return View("Profile", model);
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

                // Всегда показываем одно и то же сообщение для безопасности
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
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }

        // ViewModel для AccountController
        public class LoginViewModel
        {
            [Required(ErrorMessage = "Email обязателен")]
            [EmailAddress(ErrorMessage = "Некорректный формат Email")]
            [Display(Name = "Email")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Пароль обязателен")]
            [DataType(DataType.Password)]
            [Display(Name = "Пароль")]
            public string Password { get; set; } = string.Empty;

            [Display(Name = "Запомнить меня")]
            public bool RememberMe { get; set; }

            public string? ReturnUrl { get; set; }
        }

        public class RegisterViewModel
        {
            [Required(ErrorMessage = "Имя обязательно")]
            [Display(Name = "Имя")]
            [StringLength(50, ErrorMessage = "Имя должно содержать от {2} до {1} символов", MinimumLength = 2)]
            public string FirstName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Фамилия обязательна")]
            [Display(Name = "Фамилия")]
            [StringLength(50, ErrorMessage = "Фамилия должна содержать от {2} до {1} символов", MinimumLength = 2)]
            public string LastName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Email обязателен")]
            [EmailAddress(ErrorMessage = "Некорректный формат Email")]
            [Display(Name = "Email")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Телефон обязателен")]
            [Phone(ErrorMessage = "Некорректный формат телефона")]
            [Display(Name = "Телефон")]
            public string Phone { get; set; } = string.Empty;

            [Required(ErrorMessage = "Пароль обязателен")]
            [StringLength(100, ErrorMessage = "Пароль должен содержать от {2} до {1} символов", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Пароль")]
            public string Password { get; set; } = string.Empty;

            [DataType(DataType.Password)]
            [Display(Name = "Подтверждение пароля")]
            [Compare("Password", ErrorMessage = "Пароли не совпадают")]
            public string ConfirmPassword { get; set; } = string.Empty;

            [Required(ErrorMessage = "Необходимо согласие с условиями")]
            [Display(Name = "Я согласен с условиями использования")]
            public bool AcceptTerms { get; set; }

            [Display(Name = "Подписаться на новости")]
            public bool SubscribeToNewsletter { get; set; } = true;
        }

        public class UserProfileViewModel
        {
            public string Id { get; set; } = string.Empty;

            [Required(ErrorMessage = "Имя обязательно")]
            [Display(Name = "Имя")]
            public string FirstName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Фамилия обязательна")]
            [Display(Name = "Фамилия")]
            public string LastName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Email обязателен")]
            [EmailAddress(ErrorMessage = "Некорректный формат Email")]
            [Display(Name = "Email")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Телефон обязателен")]
            [Phone(ErrorMessage = "Некорректный формат телефона")]
            [Display(Name = "Телефон")]
            public string Phone { get; set; } = string.Empty;

            [Display(Name = "Аватар")]
            public string? AvatarUrl { get; set; }

            [Display(Name = "Дата регистрации")]
            public DateTime RegisteredAt { get; set; }

            [Display(Name = "Количество заказов")]
            public int OrderCount { get; set; }

            [Display(Name = "Адреса доставки")]
            public List<AddressViewModel> Addresses { get; set; } = new();

            public string FullName => $"{FirstName} {LastName}";
        }

        public class AddressViewModel
        {
            [Display(Name = "Название адреса")]
            public string Title { get; set; } = "Домашний адрес";

            [Required(ErrorMessage = "Адрес обязателен")]
            [Display(Name = "Адрес")]
            public string AddressLine1 { get; set; } = string.Empty;

            [Display(Name = "Дополнительная информация")]
            public string? AddressLine2 { get; set; }

            [Required(ErrorMessage = "Город обязателен")]
            [Display(Name = "Город")]
            public string City { get; set; } = string.Empty;

            [Display(Name = "Область")]
            public string? Region { get; set; }

            [Display(Name = "Почтовый индекс")]
            public string? PostalCode { get; set; }

            [Display(Name = "Страна")]
            public string Country { get; set; } = "Беларусь";

            [Display(Name = "Использовать по умолчанию")]
            public bool IsDefault { get; set; }

            public string FullAddress => $"{City}, {AddressLine1}" +
                (!string.IsNullOrEmpty(AddressLine2) ? $", {AddressLine2}" : "") +
                (!string.IsNullOrEmpty(Region) ? $", {Region}" : "");
        }

        public class ForgotPasswordViewModel
        {
            [Required(ErrorMessage = "Email обязателен")]
            [EmailAddress(ErrorMessage = "Некорректный формат Email")]
            [Display(Name = "Email")]
            public string Email { get; set; } = string.Empty;
        }

        public class ResetPasswordViewModel
        {
            [Required(ErrorMessage = "Email обязателен")]
            [EmailAddress(ErrorMessage = "Некорректный формат Email")]
            [Display(Name = "Email")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Пароль обязателен")]
            [StringLength(100, ErrorMessage = "Пароль должен содержать от {2} до {1} символов", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Пароль")]
            public string Password { get; set; } = string.Empty;

            [DataType(DataType.Password)]
            [Display(Name = "Подтверждение пароля")]
            [Compare("Password", ErrorMessage = "Пароли не совпадают")]
            public string ConfirmPassword { get; set; } = string.Empty;

            public string Token { get; set; } = string.Empty;
            public string UserId { get; set; } = string.Empty;
        }

        public class ChangePasswordViewModel
        {
            [Required(ErrorMessage = "Текущий пароль обязателен")]
            [DataType(DataType.Password)]
            [Display(Name = "Текущий пароль")]
            public string OldPassword { get; set; } = string.Empty;

            [Required(ErrorMessage = "Новый пароль обязателен")]
            [StringLength(100, ErrorMessage = "Пароль должен содержать от {2} до {1} символов", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Новый пароль")]
            public string NewPassword { get; set; } = string.Empty;

            [DataType(DataType.Password)]
            [Display(Name = "Подтверждение нового пароля")]
            [Compare("NewPassword", ErrorMessage = "Пароли не совпадают")]
            public string ConfirmPassword { get; set; } = string.Empty;
        }
    }
}