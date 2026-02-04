using EquipmentShop.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EquipmentShop.Controllers
{
    [Authorize]
    public class AddressesController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AddressesController> _logger;

        public AddressesController(
            UserManager<ApplicationUser> userManager,
            ILogger<AddressesController> logger)
        {
            _userManager = userManager;
            _logger = logger;
        }

        // GET: /addresses
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            return View(user.AdditionalAddresses ?? new List<UserAddress>());
        }

        // GET: /addresses/create
        [HttpGet]
        public IActionResult Create()
        {
            return View(new UserAddress());
        }

        // POST: /addresses/create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserAddress address)
        {
            if (!ModelState.IsValid)
            {
                return View(address);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            // Если устанавливаем как основной - снимаем флаг с других
            if (address.IsDefault)
            {
                foreach (var existing in user.AdditionalAddresses)
                {
                    existing.IsDefault = false;
                }
            }

            user.AddAddress(address);
            await _userManager.UpdateAsync(user);

            TempData["Success"] = "Адрес успешно добавлен";
            return RedirectToAction("Index");
        }

        // GET: /addresses/edit/{index}
        [HttpGet]
        public async Task<IActionResult> Edit(int index)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.AdditionalAddresses == null || index >= user.AdditionalAddresses.Count)
                return NotFound();

            var address = user.AdditionalAddresses[index];
            return View(address);
        }

        // POST: /addresses/edit/{index}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int index, UserAddress address)
        {
            if (!ModelState.IsValid)
            {
                return View(address);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.AdditionalAddresses == null || index >= user.AdditionalAddresses.Count)
                return NotFound();

            // Если устанавливаем как основной - снимаем флаг с других
            if (address.IsDefault)
            {
                foreach (var existing in user.AdditionalAddresses)
                {
                    existing.IsDefault = false;
                }
            }

            user.AdditionalAddresses[index] = address;
            await _userManager.UpdateAsync(user);

            TempData["Success"] = "Адрес успешно обновлён";
            return RedirectToAction("Index");
        }

        // POST: /addresses/delete/{index}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int index)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.AdditionalAddresses == null || index >= user.AdditionalAddresses.Count)
                return NotFound();

            user.AdditionalAddresses.RemoveAt(index);
            await _userManager.UpdateAsync(user);

            TempData["Success"] = "Адрес успешно удалён";
            return RedirectToAction("Index");
        }

        // POST: /addresses/set-default/{index}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetDefault(int index)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.AdditionalAddresses == null || index >= user.AdditionalAddresses.Count)
                return NotFound();

            // Снимаем флаг с других
            foreach (var existing in user.AdditionalAddresses)
            {
                existing.IsDefault = false;
            }

            // Устанавливаем новый
            user.AdditionalAddresses[index].IsDefault = true;
            await _userManager.UpdateAsync(user);

            TempData["Success"] = "Основной адрес обновлён";
            return RedirectToAction("Index");
        }
    }
}