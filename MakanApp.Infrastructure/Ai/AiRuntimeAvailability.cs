using MakanApp.Application.Ai;

namespace MakanApp.Infrastructure.Ai;

public sealed class AiRuntimeAvailability(AiServiceOptions options) : IAiRuntimeAvailability
{
    public void EnsureEnabled()
    {
        if (!options.Enabled) throw new AiServiceException(AiServiceErrorCodes.FeatureDisabled, "قابلیت هوش مصنوعی فعال نیست.");
    }
}
