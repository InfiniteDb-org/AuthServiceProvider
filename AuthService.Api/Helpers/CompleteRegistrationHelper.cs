using System.Text;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using AuthService.Api.Models.Responses;
using AuthService.Api.DTOs;

namespace AuthService.Api.Helpers;

public static class CompleteRegistrationHelper
{
    public static async Task<(AccountServiceResult? result, string? error)> CompleteAccountRegistrationAsync(
        CompleteRegistrationFormDto formData,
        IConfiguration configuration,
        HttpClient httpClient)
    {
        var accountServiceUrl = configuration["Providers:AccountServiceProvider"];
        var accountJson = JsonConvert.SerializeObject(formData);
        var accountContent = new StringContent(accountJson, Encoding.UTF8, "application/json");
        var request = FunctionKeyHelper.CreateRequestWithKey(
            configuration,
            HttpMethod.Post,
            $"{accountServiceUrl}/api/accounts/complete-registration",
            accountContent,
            configuration["Providers:AccountServiceProviderKey"]
        );
        var response = await httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return (null, "Failed to complete registration: " + responseContent);

        var accountResult = JsonConvert.DeserializeObject<AccountServiceResult>(responseContent);
        return (accountResult, null);
    }
}
