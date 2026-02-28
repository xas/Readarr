using System.IO;
using System.Text.RegularExpressions;
using FluentValidation;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;

namespace NzbDrone.Core.Validation.Paths
{
    public class MappedNetworkDriveValidator : AbstractValidator<string>
    {
        private readonly IRuntimeInfo _runtimeInfo;
        private readonly IDiskProvider _diskProvider;

        private static readonly Regex DriveRegex = new Regex(@"[a-z]\:\\", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public MappedNetworkDriveValidator(IRuntimeInfo runtimeInfo, IDiskProvider diskProvider)
        {
            _runtimeInfo = runtimeInfo;
            _diskProvider = diskProvider;

            RuleFor(net => net)
                .NotNull()
                .Custom((net, ctx) =>
                {
                    if (OsInfo.IsNotWindows)
                    {
                        return;
                    }

                    if (!_runtimeInfo.IsWindowsService)
                    {
                        return;
                    }

                    if (!DriveRegex.IsMatch(net))
                    {
                        return;
                    }

                    var mount = _diskProvider.GetMount(net);

                    if (mount is not { DriveType: DriveType.Network })
                    {
                        ctx.AddFailure("Mapped Network Drive and Windows Service");
                    }
                });
        }
    }
}
