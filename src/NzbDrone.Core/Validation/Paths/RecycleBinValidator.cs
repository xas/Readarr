using FluentValidation;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;

namespace NzbDrone.Core.Validation.Paths
{
    public class RecycleBinValidator : AbstractValidator<string>
    {
        private readonly IConfigService _configService;

        public RecycleBinValidator(IConfigService configService)
        {
            _configService = configService;

            RuleFor(f => f)
                .Custom((f, ctx) =>
                {
                    string recycleBin = _configService.RecycleBin;
                    if (f is null || recycleBin.IsNotNullOrWhiteSpace())
                    {
                        return;
                    }

                    if (recycleBin.PathEquals(f))
                    {
                        ctx.AddFailure("Path '{PropertyFolder}' is set configured recycle bin folder");
                    }
                    else if (recycleBin.IsParentPath(f))
                    {
                        ctx.AddFailure("Path '{PropertyFolder}' is child of configured recycle bin folder");
                    }
                });
        }
    }
}
