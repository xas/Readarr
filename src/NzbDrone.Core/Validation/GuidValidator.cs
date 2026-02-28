using System;
using FluentValidation;

namespace NzbDrone.Core.Validation
{
    public class GuidValidator : AbstractValidator<string>
    {
        public GuidValidator()
        {
            RuleFor(g => g)
                .NotNull()
                .Must(g => Guid.TryParse(g, out _));
        }
    }
}
