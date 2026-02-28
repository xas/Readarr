using FluentValidation;
using NzbDrone.Common.Disk;

namespace NzbDrone.Core.Validation.Paths
{
    public class PathExistsValidator : AbstractValidator<string>
    {
        private readonly IDiskProvider _diskProvider;

        public PathExistsValidator(IDiskProvider diskProvider)
        {
            _diskProvider = diskProvider;

            RuleFor(path => path)
                .NotNull()
                .Must(p => _diskProvider.FolderExists(p))
                .WithMessage("Path '{PropertyValue}' does not exist");
        }
    }
}
