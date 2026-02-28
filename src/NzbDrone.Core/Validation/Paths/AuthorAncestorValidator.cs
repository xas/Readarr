using System.Linq;
using FluentValidation;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;

namespace NzbDrone.Core.Validation.Paths
{
    public class AuthorAncestorValidator : AbstractValidator<string>
    {
        private readonly IAuthorService _authorService;

        public AuthorAncestorValidator(IAuthorService authorService)
        {
            _authorService = authorService;

            RuleFor(p => p)
                .Custom((p, ctx) =>
                {
                    if (p is null)
                    {
                        return;
                    }

                    if (_authorService.AllAuthorPaths().Any(s => p.IsParentPath(s.Value)))
                    {
                        ctx.AddFailure("Path '{PropertyValue}' is an ancestor of an existing author");
                    }
                });
        }
    }
}
