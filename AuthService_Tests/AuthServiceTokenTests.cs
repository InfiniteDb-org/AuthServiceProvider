using System.Text;
using AuthService.Api.DTOs;
using AuthService.Api.Functions;
using AuthService.Api.Models.Responses;
using AuthService.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RichardSzalay.MockHttp;

namespace AuthService_Tests
{
    public class AuthServiceTokenTests
    {
        [Fact]
        public async Task CompleteRegistration_ReturnsAccessToken_AndUser_WhenSuccessful()
        {
            // Arrange
            const string expectedToken = "test-token";
            const string expectedUserId = "948a7ffa-f057-413b-9fb0-a87a7e9da930";
            const string expectedEmail = "test@example.com";
            var expectedUser = new UserAccountDto
            {
                Id = Guid.Parse(expectedUserId),
                Email = expectedEmail,
                FirstName = "Test",
                LastName = "User"
            };
            var accountServiceResponse = new AccountServiceResult
            {
                Data = new AccountServiceData
                {
                    User = expectedUser
                }
            };
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("*/api/accounts/complete-registration")
                .Respond("application/json", JsonConvert.SerializeObject(accountServiceResponse));
            var httpClient = new HttpClient(mockHttp);

            var mockTokenService = new Mock<ITokenServiceClient>();
            mockTokenService
                .Setup(x => x.RequestTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((true, expectedToken));

            var logger = Mock.Of<ILogger<AuthFunctions>>();
            var config = new ConfigurationBuilder().AddInMemoryCollection([
                new KeyValuePair<string, string?>("Providers:AccountServiceProvider", "http://fake")
            ]).Build();

            var mockAuthService = new Mock<IAuthService>().Object;
            var functions = new AuthFunctions(
                logger,
                mockAuthService,
                config,
                httpClient,
                mockTokenService.Object
            );

            var formDto = new CompleteRegistrationFormDto
            {
                Email = expectedEmail,
                Password = "Test123!",
                FirstName = "Test",
                LastName = "User"
            };
            var json = JsonConvert.SerializeObject(formDto);
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Body = new MemoryStream(Encoding.UTF8.GetBytes(json)),
                    ContentType = "application/json"
                }
            };

            // Act
            var result = await functions.CompleteRegistration(context.Request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result); 
            var jsonResult = JsonConvert.SerializeObject(okResult.Value);
            var jObj = JObject.Parse(jsonResult);
            Assert.NotNull(jObj["user"]);
            Assert.False(string.IsNullOrEmpty((string?)jObj["user"]?["Id"]), "user.Id is null or empty");
            Assert.Equal(Guid.Parse(expectedUserId), Guid.Parse((string?)jObj["user"]?["Id"] ?? string.Empty));
            Assert.Equal(expectedEmail, (string?)jObj["user"]?["Email"]);
        }

        [Fact]
        public async Task CompleteRegistration_ReturnsBadRequest_WhenTokenFails()
        {
            // Arrange
            const string expectedEmail = "test@example.com";
            var expectedUser = new UserAccountDto
            {
                Email = expectedEmail,
                FirstName = "Test",
                LastName = "User"
            };
            var accountServiceResponse = new AccountServiceResult
            {
                Data = new AccountServiceData
                {
                    User = expectedUser
                }
            };
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("*/api/accounts/complete-registration")
                .Respond("application/json", JsonConvert.SerializeObject(accountServiceResponse));
            var httpClient = new HttpClient(mockHttp);

            var mockTokenService = new Mock<ITokenServiceClient>();
            mockTokenService
                .Setup(x => x.RequestTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((false, null)); 

            var logger = Mock.Of<ILogger<AuthFunctions>>();
            var config = new ConfigurationBuilder().AddInMemoryCollection([
                new KeyValuePair<string, string?>("Providers:AccountServiceProvider", "http://fake")
            ]).Build();

            var mockAuthService = new Mock<IAuthService>().Object;
            var functions = new AuthFunctions(
                logger,
                mockAuthService,
                config,
                httpClient,
                mockTokenService.Object
            );

            var formDto = new CompleteRegistrationFormDto
            {
                Email = expectedEmail,
                Password = "Test123!",
                FirstName = "Test",
                LastName = "User"
            };
            var json = JsonConvert.SerializeObject(formDto);
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Body = new MemoryStream(Encoding.UTF8.GetBytes(json)),
                    ContentType = "application/json"
                }
            };

            // Act
            var result = await functions.CompleteRegistration(context.Request);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = badRequest.Value?.ToString() ?? string.Empty;
            Assert.Contains("userId could not be extracted", message);
        }

        [Fact]
        public async Task CompleteRegistration_ReturnsBadRequest_WhenUserIsNull()
        {
            // Arrange
            const string expectedEmail = "test@example.com";
            var accountServiceResponse = new AccountServiceResult
            {
                Data = new AccountServiceData
                {
                    User = null
                }
            };
            var mockHttp = new MockHttpMessageHandler();
            mockHttp.When("*/api/accounts/complete-registration")
                .Respond("application/json", JsonConvert.SerializeObject(accountServiceResponse));
            var httpClient = new HttpClient(mockHttp);

            var mockTokenService = new Mock<ITokenServiceClient>();
            mockTokenService
                .Setup(x => x.RequestTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((true, "test-token"));

            var logger = Mock.Of<ILogger<AuthFunctions>>();
            var config = new ConfigurationBuilder().AddInMemoryCollection([
                new KeyValuePair<string, string?>("Providers:AccountServiceProvider", "http://fake")
            ]).Build();

            var mockAuthService = new Mock<IAuthService>().Object;
            var functions = new AuthFunctions(
                logger,
                mockAuthService,
                config,
                httpClient,
                mockTokenService.Object
            );

            var formDto = new CompleteRegistrationFormDto
            {
                Email = expectedEmail,
                Password = "Test123!",
                FirstName = "Test",
                LastName = "User"
            };
            var json = JsonConvert.SerializeObject(formDto);
            var context = new DefaultHttpContext
            {
                Request =
                {
                    Body = new MemoryStream(Encoding.UTF8.GetBytes(json)),
                    ContentType = "application/json"
                }
            };

            // Act
            var result = await functions.CompleteRegistration(context.Request);

            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = badRequest.Value?.ToString() ?? string.Empty;
            Assert.Contains("userId could not be extracted", message);
        }
    }
}
