using FluentValidation;

namespace Readarr.Http.Validation
{
    public class RssSyncIntervalValidator : AbstractValidator<int>
    {
        public RssSyncIntervalValidator()
        {
            RuleFor(v => v)
                .IsZero()
                .InclusiveBetween(10, 120)
                .WithMessage("Must be between 10 and 120 or 0 to disable");
        }
    }
}
