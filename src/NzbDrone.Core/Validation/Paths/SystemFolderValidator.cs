using FluentValidation;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Validation.Paths
{
    public class SystemFolderValidator : AbstractValidator<string>
    {
        public SystemFolderValidator()
        {
            var systemFolders = SystemFolders.GetSystemFolders();

            RuleFor(p => p)
                .Custom((p, ctx) =>
                {
                    foreach (string systemFolder in systemFolders)
                    {
                        if (systemFolder.PathEquals(p))
                        {
                            ctx.AddFailure($"Path '{p}' is set to system folder {systemFolder}");
                            break;
                        }

                        if (systemFolder.IsParentPath(p))
                        {
                            ctx.AddFailure($"Path '{p}' is child of system folder {systemFolder}");
                            break;
                        }
                    }
                });
        }
    }
}
