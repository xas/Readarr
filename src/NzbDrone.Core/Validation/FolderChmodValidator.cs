using FluentValidation;
using NzbDrone.Common.Disk;

namespace NzbDrone.Core.Validation
{
    public class FolderChmodValidator : AbstractValidator<string>
    {
        private readonly IDiskProvider _diskProvider;

        public FolderChmodValidator(IDiskProvider diskProvider)
        {
            _diskProvider = diskProvider;

            RuleFor(folder => folder)
                .NotNull()
                .Must(f => _diskProvider.IsValidFolderPermissionMask(f))
                .WithMessage("Must contain a valid Unix permissions octal");
        }
    }
}
