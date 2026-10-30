using MakanApp.Application.Realtime;
using Microsoft.AspNetCore.DataProtection;

namespace MakanApp.Api.Realtime;

public sealed class RealtimeCursorProtector(IDataProtectionProvider provider) : IRealtimeCursorProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("MakanApp.AccountReplay.v1");
    public string Protect(string value) => _protector.Protect(value);
    public string Unprotect(string value) => _protector.Unprotect(value);
}
