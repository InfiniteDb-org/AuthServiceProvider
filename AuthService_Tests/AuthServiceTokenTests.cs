using System.Net;
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
using Moq.Protected;
using Newtonsoft.Json;

namespace AuthService_Tests
{
    public class AuthServiceTokenTests
    {
        private static UserAccountDto CreateTestUser(string id, string email) => new()
        {
            Id = Guid.Parse(id),
            Email = email,
            FirstName = "Test",
            LastName = "User"
        };

        // returns given response as JSON
        private static HttpClient CreateMockHttpClient(object response)
        {
            var mockHttp = new Mock<HttpMessageHandler>();
            mockHttp.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonConvert.SerializeObject(response))
                });
            return new HttpClient(mockHttp.Object);
        }

        // creates a mock IConfiguration with required keys
        private static Mock<IConfiguration> CreateMockConfig() {
            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(x => x["Providers:AccountServiceProvider"]).Returns("http://localhost");
            return mockConfig;
        }

        [Fact]
        public async Task CompleteRegistration_ReturnsAccessToken_AndUser_WhenSuccessful()
        {
            // Arrange
            const string expectedToken = "test-token-123";
            const string expectedUserId = "948a7ffa-f057-413b-9fb0-a87a7e9da930";
            const string expectedEmail = "test@example.com";
            
            var expectedUser = CreateTestUser(expectedUserId, expectedEmail);
            var accountServiceResponse = new { Data = new { User = expectedUser } };
            var httpClient = CreateMockHttpClient(accountServiceResponse);
            var mockTokenService = new Mock<ITokenServiceClient>();
            
            mockTokenService.Setup(x => x.RequestTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new TokenResult {Succeeded = true, AccessToken = expectedToken, RefreshToken = "test-refresh-token" });
            
            var functions = new AuthFunctions(Mock.Of<ILogger<AuthFunctions>>(), new Mock<IAuthService>().Object,
                CreateMockConfig().Object,
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
            var context = new DefaultHttpContext { Request = { Body = new MemoryStream(
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(formDto))), ContentType = "application/json" } };
            
            // Act
            var result = await functions.CompleteRegistration(context.Request);
            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var jsonResult = JsonConvert.SerializeObject(okResult.Value);
            Assert.Contains("accessToken", jsonResult);
            Assert.Contains("refreshToken", jsonResult);
            Assert.Contains(expectedUserId, jsonResult);
            Assert.Contains(expectedEmail, jsonResult);
        }

        [Fact]
        public async Task CompleteRegistration_ReturnsBadRequest_WhenTokenFails()
        {
            // Arrange
            const string expectedUserId = "948a7ffa-f057-413b-9fb0-a87a7e9da930";
            const string expectedEmail = "test@example.com";
            
            var expectedUser = CreateTestUser(expectedUserId, expectedEmail);
            var accountServiceResponse = new { Data = new { User = expectedUser } };
            var httpClient = CreateMockHttpClient(accountServiceResponse);
            var mockTokenService = new Mock<ITokenServiceClient>();
            
            mockTokenService.Setup(x => x.RequestTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new TokenResult {Succeeded = false, AccessToken = null, RefreshToken = null });
            
            var functions = new AuthFunctions(
                Mock.Of<ILogger<AuthFunctions>>(),
                new Mock<IAuthService>().Object,
                CreateMockConfig().Object,
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
            var context = new DefaultHttpContext { Request = { Body = new MemoryStream(
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(formDto))), ContentType = "application/json" } };
            
            // Act
            var result = await functions.CompleteRegistration(context.Request);
            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = badRequest.Value?.ToString() ?? string.Empty;
            Assert.Contains("Internal server error: Could not generate access token.", message);
        }

        [Fact]
        public async Task CompleteRegistration_ReturnsBadRequest_WhenUserIsNull()
        {
            // Arrange
            var accountServiceResponse = new { Data = new { User = (UserAccountDto?)null } };
            var httpClient = CreateMockHttpClient(accountServiceResponse);
            var mockTokenService = new Mock<ITokenServiceClient>();
            
            mockTokenService.Setup(x => x.RequestTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new TokenResult { Succeeded = false, AccessToken = null, RefreshToken = null });
            
            var functions = new AuthFunctions( Mock.Of<ILogger<AuthFunctions>>(), new Mock<IAuthService>().Object,
                CreateMockConfig().Object,
                httpClient,
                mockTokenService.Object
            );
            var formDto = new CompleteRegistrationFormDto { Email = "test@example.com", Password = "Test123!", FirstName = "Test", LastName = "User" };
            
            var context = new DefaultHttpContext { Request = { Body = new MemoryStream(
                Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(formDto))), ContentType = "application/json" } };
            
            // Act
            var result = await functions.CompleteRegistration(context.Request);
            // Assert
            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var message = badRequest.Value?.ToString() ?? string.Empty;
            Assert.Contains("userId could not be extracted", message);
        }

        [Fact]
        public async Task SignInAsync_Returns_Valid_RefreshToken_When_Successful()
        {
            // Arrange
            const string expectedAccessToken = "access-token-123";
            const string expectedRefreshToken = "refresh-token-abc";
            var expectedUser = CreateTestUser(Guid.NewGuid().ToString(), "test@example.com");
            
            var accountServiceResponse = new { data = new { user = expectedUser } };
            var httpClient = CreateMockHttpClient(accountServiceResponse);
            var mockTokenService = new Mock<ITokenServiceClient>();
            
            mockTokenService.Setup(x => x.RequestTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new TokenResult { Succeeded = true, AccessToken = expectedAccessToken, RefreshToken = expectedRefreshToken });
            
            var mockLogger = new Mock<ILogger<AuthService.Api.Services.AuthService>>();
            var mockConfig = CreateMockConfig();
            var authService = new AuthService.Api.Services.AuthService(httpClient, mockConfig.Object, mockLogger.Object, mockTokenService.Object);
            var dto = new SignInFormDto { Email = "test@example.com", Password = "pw" };
            
            // Act
            var result = await authService.SignInAsync(dto);
            // Assert
            Assert.True(result.Succeeded);
            Assert.Equal(expectedAccessToken, result.AccessToken);
            Assert.Equal(expectedRefreshToken, result.RefreshToken);
        }

        [Fact]
        public async Task SignInAsync_Returns_Null_RefreshToken_When_Failed()
        {
            // Arrange
            var mockTokenService = new Mock<ITokenServiceClient>();
            
            mockTokenService.Setup(x => x.RequestTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new TokenResult { Succeeded = false, AccessToken = null, RefreshToken = null });
            
            var mockLogger = new Mock<ILogger<AuthService.Api.Services.AuthService>>();
            var mockConfig = CreateMockConfig();
            var httpClient = CreateMockHttpClient(new { data = new { user = CreateTestUser(Guid.NewGuid().ToString(), "fail@example.com") } });
            var authService = new AuthService.Api.Services.AuthService(httpClient, mockConfig.Object, mockLogger.Object, mockTokenService.Object);
            var dto = new SignInFormDto { Email = "fail@example.com", Password = "pw" };
            
            // Act
            var result = await authService.SignInAsync(dto);
            // Assert
            Assert.False(result.Succeeded);
            Assert.Null(result.AccessToken);
            Assert.Null(result.RefreshToken);
        }

        [Fact]
        public async Task SignInAsync_Handles_Exception_From_TokenServiceClient()
        {
            // Arrange
            var expectedUser = CreateTestUser(Guid.NewGuid().ToString(), "fail2@example.com");

            var accountServiceResponse = new { data = new { user = expectedUser } };
            var httpClient = CreateMockHttpClient(accountServiceResponse);
            var mockTokenService = new Mock<ITokenServiceClient>();
            
            mockTokenService.Setup(x => x.RequestTokenAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Handler did not return a response"));
            
            var mockLogger = new Mock<ILogger<AuthService.Api.Services.AuthService>>();
            var mockConfig = CreateMockConfig();
            var authService = new AuthService.Api.Services.AuthService(httpClient, mockConfig.Object, mockLogger.Object, mockTokenService.Object);
            var dto = new SignInFormDto { Email = "fail2@example.com", Password = "pw" };
            
            // Act
            var result = await authService.SignInAsync(dto);
            // Assert
            Assert.False(result.Succeeded);
            Assert.Contains("Error:", result.Message);
            Assert.Null(result.AccessToken);
            Assert.Null(result.RefreshToken);
        }
    }
}
