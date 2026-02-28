using FluentValidation;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Validation
{
    public class FolderValidator : AbstractValidator<string>
    {
        public FolderValidator()
        {
            RuleFor(folder => folder)
                .NotNull()
                .Must(f => f.IsPathValid(PathValidationType.CurrentOs))
                .WithMessage("Invalid Path: '{PropertyValue}'");
        }
    }
}
