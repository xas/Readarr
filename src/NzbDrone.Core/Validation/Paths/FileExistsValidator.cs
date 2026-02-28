using FluentValidation;
using NzbDrone.Common.Disk;

namespace NzbDrone.Core.Validation.Paths
{
    public class FileExistsValidator : AbstractValidator<string>
    {
        private readonly IDiskProvider _diskProvider;

        public FileExistsValidator(IDiskProvider diskProvider)
        {
            _diskProvider = diskProvider;

            RuleFor(file => file)
                .NotNull()
                .Must(f => _diskProvider.FileExists(f))
                .WithMessage("File '{PropertyValue}' does not exist");
        }
    }
}
