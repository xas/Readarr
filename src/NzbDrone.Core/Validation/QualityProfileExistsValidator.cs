using FluentValidation;
using NzbDrone.Core.Profiles.Qualities;

namespace NzbDrone.Core.Validation
{
    public class QualityProfileExistsValidator : AbstractValidator<int>
    {
        private readonly IQualityProfileService _qualityProfileService;

        public QualityProfileExistsValidator(IQualityProfileService qualityProfileService)
        {
            _qualityProfileService = qualityProfileService;

            RuleFor(q => q)
                .Must(q => _qualityProfileService.Exists(q))
                .When(q => q != 0)
                .WithMessage("Quality Profile does not exist");
        }
    }
}
