using FluentValidation;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Validation.Paths
{
    public class StartupFolderValidator : AbstractValidator<string>
    {
        private readonly IAppFolderInfo _appFolderInfo;

        public StartupFolderValidator(IAppFolderInfo appFolderInfo)
        {
            _appFolderInfo = appFolderInfo;

            RuleFor(p => p)
                .Custom((p, ctx) =>
                {
                    string startupFolder = _appFolderInfo.StartUpFolder;
                    if (startupFolder.PathEquals(p))
                    {
                        ctx.AddFailure($"Path '{p}' cannot be set to the start up folder");
                    }

                    if (startupFolder.IsParentPath(p))
                    {
                        ctx.AddFailure($"Path '{p}' is child of the start up folder");
                    }
                });
        }
    }
}
