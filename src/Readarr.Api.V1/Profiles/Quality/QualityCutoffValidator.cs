using System.Linq;
using FluentValidation;
using FluentValidation.Validators;

namespace Readarr.Api.V1.Profiles.Quality
{
    public static class QualityCutoffValidator
    {
        public static IRuleBuilderOptions<T, int> ValidCutoff<T>(this IRuleBuilder<T, int> ruleBuilder)
        {
            return ruleBuilder.SetValidator(new ValidCutoffValidator<T>());
        }
    }

    public class ValidCutoffValidator<T> : PropertyValidator<T, int>
    {
        public override string Name => "ValidCutoffValidator";

        public override bool IsValid(ValidationContext<T> context, int value)
        {
            if (context.InstanceToValidate is QualityProfileResource quality)
            {
                var cutoffItem = quality.Items.SingleOrDefault(i => (i.Quality == null && i.Id == value) || i.Quality?.Id == value);
                return cutoffItem is { Allowed: true };
            }

            return false;
        }

        protected override string GetDefaultMessageTemplate(string errorCode) => $"{errorCode}: Cutoff must be an allowed quality or group";
    }
}
