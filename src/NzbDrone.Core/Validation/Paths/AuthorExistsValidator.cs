using FluentValidation;
using NzbDrone.Core.Books;

namespace NzbDrone.Core.Validation.Paths
{
    public class AuthorExistsValidator : AbstractValidator<string>
    {
        private readonly IAuthorService _authorService;

        public AuthorExistsValidator(IAuthorService authorService)
        {
            _authorService = authorService;

            RuleFor(a => a)
                .Must(a => _authorService.FindById(a) is null)
                .When(a => a is not null)
                .WithMessage("This author has already been added");
        }
    }
}
