using FluentValidation;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Validation
{
    public static class UrlValidation
    {
        public static IRuleBuilderOptions<T, string> IsValidUrl<T>(this IRuleBuilder<T, string> ruleBuilder)
        {
            return ruleBuilder.SetValidator(new UrlValidator());
        }
    }

    public class UrlValidator : AbstractValidator<string>
    {
        public UrlValidator()
        {
            RuleFor(s => s)
                .Must(url => url.IsValidUrl())
                .When(url => !string.IsNullOrEmpty(url))
                .WithMessage("Invalid Url: '{PropertyValue}'");
        }
    }
}
