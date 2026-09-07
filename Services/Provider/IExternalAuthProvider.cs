using BE_ZSM.Enums;
using BE_ZSM.Services.Auth.Models;

namespace BE_ZSM.Services.Provider;

public interface IExternalAuthProvider
{
    AuthProvider Provider { get; }
    Task<ExternalUserInfo> ValidateAsync(string token);
}
