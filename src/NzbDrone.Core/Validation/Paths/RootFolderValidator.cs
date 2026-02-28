using FluentValidation;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.RootFolders;

namespace NzbDrone.Core.Validation.Paths
{
    public class RootFolderValidator : AbstractValidator<string>
    {
        private readonly IRootFolderService _rootFolderService;

        public RootFolderValidator(IRootFolderService rootFolderService)
        {
            _rootFolderService = rootFolderService;

            RuleFor(r => r)
                .Must(root => !_rootFolderService.All().Exists(r => r.Path.PathEquals(root)))
                .When(root => root is not null)
                .WithMessage("Path '{ProperryValue}' is already configured as a root folder");
        }
    }
}
