using System.ComponentModel.DataAnnotations;

namespace EquipmentShop.UnitTests.ViewModels
{
    public class LoginViewModelTests
    {
        [Fact]
        public void LoginViewModel_ValidData_PassesValidation()
        {
            var m = new LoginViewModel { Email = "t@test.com", Password = "Password123" };
            var c = new ValidationContext(m); var r = new List<ValidationResult>();
            Validator.TryValidateObject(m, c, r, true).Should().BeTrue(); r.Should().BeEmpty();
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("invalid")]
        public void LoginViewModel_InvalidEmail_FailsValidation(string e)
        {
            var m = new LoginViewModel { Email = e, Password = "Password123" };
            var c = new ValidationContext(m); var r = new List<ValidationResult>();
            Validator.TryValidateObject(m, c, r, true); r.Should().Contain(x => x.MemberNames.Contains("Email"));
        }
    }
}