using System.Text;
using AuthService.Api.DTOs;
using AuthService.Api.Services;
using AuthService.Api.Helpers;
using AuthService.Api.Models.Requests;
using AuthService.Api.Models.Responses;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace AuthService.Api.Functions;

public class AuthFunctions(ILogger<AuthFunctions> logger, IAuthService authService, IConfiguration configuration, HttpClient httpClient, ITokenServiceClient tokenServiceClient)
{
    private readonly ILogger<AuthFunctions> _logger = logger;
    private readonly IAuthService _authService = authService;
    private readonly IConfiguration _configuration = configuration;
    private readonly HttpClient _httpClient = httpClient;
    private readonly ITokenServiceClient _tokenServiceClient = tokenServiceClient;

    [Function("SignUp")]
    public async Task<IActionResult> SignUp(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/signup")] HttpRequest req)
    {
        try
        {
            var (succeeded, formData, message) = await RequestBodyHelper.ReadAndValidateRequestBody<SignUpFormDto>(req, _logger);
            if (!succeeded)
                return ActionResultHelper.BadRequest(message);

            var result = await _authService.SignUpAsync(formData!);
            return ActionResultHelper.CreateResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SignUp function");
            return ActionResultHelper.BadRequest("Internal server error");
        }
    }
    
    [Function("CompleteRegistration")]
    public async Task<IActionResult> CompleteRegistration(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/complete-registration")] HttpRequest req)
    {
        try
        {
            var (succeeded, formData, message) = await RequestBodyHelper.ReadAndValidateRequestBody<CompleteRegistrationFormDto>(req, _logger);
            if (!succeeded)
                return ActionResultHelper.BadRequest(message);

            var (accountResult, error) = await CompleteRegistrationHelper.CompleteAccountRegistrationAsync(formData, _configuration, _httpClient);
            if (accountResult == null)
                return ActionResultHelper.BadRequest(error);

            var userId = accountResult.Data?.User?.Id?.ToString();
            if (string.IsNullOrEmpty(userId))
                return ActionResultHelper.BadRequest("userId could not be extracted");

            var tokenResult = await _tokenServiceClient.RequestTokenAsync(userId, formData.Email);
            if (!tokenResult.Succeeded || string.IsNullOrEmpty(tokenResult.AccessToken))
                return ActionResultHelper.BadRequest("Internal server error: Could not generate access token.");

            return ActionResultHelper.Ok(new { succeeded = true, message = "Registration complete.", accessToken = tokenResult.AccessToken, user = accountResult.Data?.User });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in CompleteRegistration function");
            return ActionResultHelper.BadRequest("Internal server error");
        }
    }
    

    [Function("SignIn")]
    public async Task<IActionResult> SignIn(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/signin")] HttpRequest req)
    {
        try
        {
            var (succeeded, formData, message) = await RequestBodyHelper.ReadAndValidateRequestBody<SignInFormDto>(req, _logger);
            if (!succeeded)
                return ActionResultHelper.BadRequest(message); 

            var result = await _authService.SignInAsync(formData!);
            return ActionResultHelper.CreateResponse(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SignIn function");
            return ActionResultHelper.BadRequest("Internal server error");
        }
    }

    [Function("SignOut")]
    public async Task<IActionResult> SignOut(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "auth/signout")] HttpRequest req)
    {
        try
        {
            var authHeader = req.Headers.Authorization.ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                return new UnauthorizedResult();

            var (succeeded, data, _) = await RequestBodyHelper.ReadAndValidateRequestBody<SignOutRequest>(req, _logger);
            if (!succeeded || string.IsNullOrEmpty(data?.UserId))
                return ActionResultHelper.BadRequest("UserId is required");

            var result = await _authService.SignOutAsync(data.UserId);
            if (!result)
                return ActionResultHelper.BadRequest("Sign out failed");

            return ActionResultHelper.Ok(new { message = "Signed out successfully", Succeeded = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SignOut function");
            return ActionResultHelper.BadRequest("Internal server error");
        }
    }
}
