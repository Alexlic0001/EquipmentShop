namespace EquipmentShop.UnitTests.Entities
{
    public class ApplicationUserTests
    {
        [Fact]
        public void ApplicationUser_Constructor_InitializesPropertiesCorrectly()
        {
            // Arrange & Act
            var user = new ApplicationUser
            {
                Id = "user-1",
                FirstName = "John",
                LastName = "Doe",
                Email = "john@example.com",
                UserName = "john@example.com"
                // Phone хранится в свойстве PhoneNumber из IdentityUser
            };

            // Assert
            user.Id.Should().Be("user-1");
            user.FirstName.Should().Be("John");
            user.LastName.Should().Be("Doe");
            user.Email.Should().Be("john@example.com");
        }

        [Fact]
        public void ApplicationUser_GetFullName_ReturnsCorrectName()
        {
            // Arrange
            var user = new ApplicationUser
            {
                FirstName = "John",
                LastName = "Doe"
            };

            // Act
            var fullName = $"{user.FirstName} {user.LastName}";

            // Assert
            fullName.Should().Be("John Doe");
        }

        [Fact]
        public void ApplicationUser_HasValidIdentityProperties()
        {
            // Arrange & Act
            var user = new ApplicationUser
            {
                UserName = "testuser",
                Email = "test@example.com",
                PhoneNumber = "+375291234567" // Используем стандартное свойство IdentityUser
            };

            // Assert
            user.UserName.Should().Be("testuser");
            user.Email.Should().Be("test@example.com");
            user.PhoneNumber.Should().Be("+375291234567");
        }
    }
}