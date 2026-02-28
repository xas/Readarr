using System.Linq;
using FluentValidation;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.RootFolders;

namespace NzbDrone.Core.Validation.Paths
{
    public class RootFolderAncestorValidator : AbstractValidator<string>
    {
        private readonly IRootFolderService _rootFolderService;

        public RootFolderAncestorValidator(IRootFolderService rootFolderService)
        {
            _rootFolderService = rootFolderService;

            RuleFor(f => f)
                .Must((f, ctx) => !_rootFolderService.All().Any(s => f.IsParentPath(s.Path)))
                .When(f => f is not null)
                .WithMessage("Path '{PropertyValue}' is an ancestor of an existing root folder");
        }
    }
}
