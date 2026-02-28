using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentValidation;
using FluentValidation.Validators;

namespace NzbDrone.Core.Organizer
{
    public static class FileNameValidation
    {
        internal static readonly Regex OriginalTokenRegex = new Regex(@"(\{original[- ._](?:title|filename)\})",
                                                                            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static IRuleBuilderOptions<T, string> ValidBookFormat<T>(this IRuleBuilder<T, string> ruleBuilder)
        {
            ruleBuilder.SetValidator(new NotEmptyValidator<T, string>());
            ruleBuilder.SetValidator(new IllegalCharactersValidator());

            return ruleBuilder.SetValidator(new ValidStandardTrackFormatValidator());
        }

        public static IRuleBuilderOptions<T, string> ValidAuthorFolderFormat<T>(this IRuleBuilder<T, string> ruleBuilder)
        {
            ruleBuilder.SetValidator(new NotEmptyValidator<T, string>());
            ruleBuilder.SetValidator(new IllegalCharactersValidator());

            return ruleBuilder.SetValidator(new RegularExpressionValidator<T>(FileNameBuilder.AuthorNameRegex)).WithMessage("Must contain Author name");
        }
    }

    public class ValidStandardTrackFormatValidator : AbstractValidator<string>
    {
        public ValidStandardTrackFormatValidator()
        {
            RuleFor(s => s)
                .NotNull()
                .Must(v => (FileNameBuilder.BookTitleRegex.IsMatch(v) && FileNameBuilder.PartRegex.IsMatch(v)) ||
                            FileNameValidation.OriginalTokenRegex.IsMatch(v))
                .WithMessage("Must contain Book Title AND PartNumber, OR Original Title");
        }
    }

    public class IllegalCharactersValidator : AbstractValidator<string>
    {
        private readonly char[] _invalidPathChars = Path.GetInvalidPathChars();

        public IllegalCharactersValidator()
        {
            RuleFor(x => x)
                .Must(v => !_invalidPathChars.Any(x => v.Contains(x, StringComparison.OrdinalIgnoreCase)))
                .When(v => !string.IsNullOrEmpty(v))
                .WithMessage("Contains illegal characters: {PropertyValue}");
        }
    }
}
