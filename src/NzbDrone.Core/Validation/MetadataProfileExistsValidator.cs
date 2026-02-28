using FluentValidation;
using NzbDrone.Core.Profiles.Metadata;

namespace NzbDrone.Core.Validation
{
    public class MetadataProfileExistsValidator : AbstractValidator<int>
    {
        private readonly IMetadataProfileService _profileService;

        public MetadataProfileExistsValidator(IMetadataProfileService profileService)
        {
            _profileService = profileService;

            RuleFor(md => md)
                .Must(md => _profileService.Exists(md))
                .When(md => md != 0)
                .WithMessage("Metadata profile does not exist");
        }
    }
}
