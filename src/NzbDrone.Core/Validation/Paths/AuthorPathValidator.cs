using System;
using System.Linq;
using FluentValidation;
using FluentValidation.Validators;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;

namespace NzbDrone.Core.Validation.Paths
{
    public static class AuthorPathValidation
    {
        /// <summary>
        /// Path must not already be used by an author, except the one returned by <paramref name="getAuthorId"/>
        /// (the author being edited). Without it, every existing author path is rejected.
        /// </summary>
        public static IRuleBuilderOptions<T, string> SetValidator<T>(this IRuleBuilder<T, string> ruleBuilder, AuthorPathValidator validator, Func<T, int> getAuthorId = null)
        {
            return ruleBuilder.SetValidator(new AuthorPathValidator<T>(validator, getAuthorId));
        }
    }

    public class AuthorPathValidator
    {
        private readonly IAuthorService _authorService;

        public AuthorPathValidator(IAuthorService authorService)
        {
            _authorService = authorService;
        }

        public bool IsAvailable(string path, int excludedAuthorId)
        {
            return !_authorService.AllAuthorPaths().Any(s => s.Value.PathEquals(path) && s.Key != excludedAuthorId);
        }
    }

    public class AuthorPathValidator<T> : PropertyValidator<T, string>
    {
        private readonly AuthorPathValidator _validator;
        private readonly Func<T, int> _getAuthorId;

        public AuthorPathValidator(AuthorPathValidator validator, Func<T, int> getAuthorId)
        {
            _validator = validator;
            _getAuthorId = getAuthorId;
        }

        public override string Name => "AuthorPathValidator";

        protected override string GetDefaultMessageTemplate(string errorCode) => "Path '{path}' is already configured for another author";

        public override bool IsValid(ValidationContext<T> context, string value)
        {
            if (value == null)
            {
                return true;
            }

            context.MessageFormatter.AppendArgument("path", value);

            var authorId = _getAuthorId?.Invoke(context.InstanceToValidate) ?? 0;

            return _validator.IsAvailable(value, authorId);
        }
    }
}
