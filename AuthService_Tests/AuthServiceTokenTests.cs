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
using Xunit.Abstractions;

namespace AuthService_Tests
{
    public class AuthServiceTokenTests(ITestOutputHelper testOutputHelper)
    {
        private readonly ITestOutputHelper _testOutputHelper = testOutputHelper;

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
                    UserId = expectedUserId,
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

            var functions = new AuthFunctions(
                logger,
                null, 
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
            var responseJson = JsonConvert.SerializeObject(okResult.Value);
            _testOutputHelper.WriteLine(responseJson);
            var jObj = JsonConvert.DeserializeObject<JObject>(responseJson);

            Assert.Equal(expectedToken, (string)jObj["accessToken"]);
            Assert.NotNull(jObj["user"]);
            Assert.False(string.IsNullOrEmpty((string)jObj["user"]["Id"]), "user.Id is null or empty");
            Assert.Equal(Guid.Parse(expectedUserId), Guid.Parse((string)jObj["user"]["Id"]));
            Assert.Equal(expectedEmail, (string)jObj["user"]["Email"]);
        }

        [Fact]
        public async Task CompleteRegistration_ReturnsBadRequest_WhenTokenFails()
        {
            // Arrange
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
                    UserId = expectedUserId,
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
                .ReturnsAsync((false, null)); // Simulera token-fel

            var logger = Mock.Of<ILogger<AuthFunctions>>();
            var config = new ConfigurationBuilder().AddInMemoryCollection([
                new KeyValuePair<string, string?>("Providers:AccountServiceProvider", "http://fake")
            ]).Build();

            var functions = new AuthFunctions(
                logger,
                null,
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
            Assert.Contains("Internal server error", badRequest.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
    }
}
