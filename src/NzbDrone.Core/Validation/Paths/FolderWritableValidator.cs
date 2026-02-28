using FluentValidation;
using NzbDrone.Common.Disk;

namespace NzbDrone.Core.Validation.Paths
{
    public class FolderWritableValidator : AbstractValidator<string>
    {
        private readonly IDiskProvider _diskProvider;

        public FolderWritableValidator(IDiskProvider diskProvider)
        {
            _diskProvider = diskProvider;

            RuleFor(folder => folder)
                .NotNull()
                .Must(f => _diskProvider.FolderWritable(f))
                .WithMessage("Folder '{PropertyValue}' is not writable by user");
        }
    }
}
