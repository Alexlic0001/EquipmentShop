using EquipmentShop.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EquipmentShop.Controllers
{
    [Authorize]
    public class AddressesController(UserManager<ApplicationUser> userManager, ILogger<AddressesController> logger) : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager = userManager;
        private readonly ILogger<AddressesController> _logger = logger;

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await GetUserWithAddressesAsync();
            return user == null ? NotFound() : View(user.AdditionalAddresses ?? new List<UserAddress>());
        }

        [HttpGet]
        public IActionResult Create() => View(new UserAddress());

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserAddress address)
        {
            if (!ModelState.IsValid) return View(address);

            var user = await GetUserWithAddressesAsync();
            if (user == null) return NotFound();

            await UpdateAddressAsync(user, address, () => user.AddAddress(address));
            TempData["Success"] = "Адрес успешно добавлен";
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int index)
        {
            (ApplicationUser user, UserAddress address) = await GetAddressAsync(index);
            return address == null ? NotFound() : View(address);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int index, UserAddress address)
        {
            if (!ModelState.IsValid) return View(address);

            var (user, _) = await GetAddressAsync(index);
            if (user == null) return NotFound();

            await UpdateAddressAsync(user, address, () => user.AdditionalAddresses[index] = address);
            TempData["Success"] = "Адрес успешно обновлён";
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int index)
        {
            var (user, _) = await GetAddressAsync(index);
            if (user == null) return NotFound();

            user.AdditionalAddresses.RemoveAt(index);
            await _userManager.UpdateAsync(user);
            TempData["Success"] = "Адрес успешно удалён";
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SetDefault(int index)
        {
            var (user, address) = await GetAddressAsync(index);
            if (address == null) return NotFound();

            await UpdateAddressAsync(user: user, new UserAddress { IsDefault = true },
                () => address.IsDefault = true);
            TempData["Success"] = "Основной адрес обновлён";
            return RedirectToAction("Index");
        }

        // === Вспомогательные методы ===
        private async Task<ApplicationUser?> GetUserWithAddressesAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.AdditionalAddresses == null)
                user.AdditionalAddresses = [];
            return user;
        }

        private async Task<(ApplicationUser? User, UserAddress? Address)> GetAddressAsync(int index)
        {
            var user = await GetUserWithAddressesAsync();
            if (user == null || index >= user.AdditionalAddresses.Count)
                return (null, null);

            return (user, user.AdditionalAddresses[index]);
        }

        private async Task UpdateAddressAsync(ApplicationUser user, UserAddress address, Action updateAction)
        {
            if (address.IsDefault)
                ClearDefaultFlags(user.AdditionalAddresses);

            updateAction();
            await _userManager.UpdateAsync(user);
        }

        private void ClearDefaultFlags(List<UserAddress> addresses)
        {
            foreach (var existing in addresses)
                existing.IsDefault = false;
        }
    }
}