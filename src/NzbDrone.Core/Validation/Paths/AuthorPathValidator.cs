using System.Linq;
using FluentValidation;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;

namespace NzbDrone.Core.Validation.Paths
{
    public class AuthorPathValidator : AbstractValidator<string>
    {
        private readonly IAuthorService _authorService;

        public AuthorPathValidator(IAuthorService authorService)
        {
            _authorService = authorService;

            RuleFor(p => p)
                .Custom((p, ctx) =>
                {
                    if (p is null)
                    {
                        return;
                    }

                    // TODO : Verify code
                    if (_authorService.AllAuthorPaths().Any(s => s.Value.PathEquals(p)))
                    {
                        ctx.AddFailure("Path '{PropertyValue}' is already configured for another author");
                    }
                });
        }
    }
}
