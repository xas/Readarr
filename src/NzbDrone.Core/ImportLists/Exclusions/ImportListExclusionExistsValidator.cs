using FluentValidation;

namespace NzbDrone.Core.ImportLists.Exclusions
{
    public class ImportListExclusionExistsValidator : AbstractValidator<string>
    {
        private readonly IImportListExclusionService _importListExclusionService;

        public ImportListExclusionExistsValidator(IImportListExclusionService importListExclusionService)
        {
            _importListExclusionService = importListExclusionService;

            RuleFor(i => i)
                .Must(i => !_importListExclusionService.All().Exists(s => s.ForeignId == i))
                .When(i => i is not null)
                .WithMessage("This exclusion has already been added.");
        }
    }
}
