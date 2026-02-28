using System.Collections.Generic;
using FluentValidation;
using NzbDrone.Common.Extensions;

namespace Readarr.Http.Validation
{
    public class EmptyCollectionValidator<T> : AbstractValidator<IEnumerable<T>>
    {
        public EmptyCollectionValidator()
        {
            RuleFor(c => c)
                .NotNull()
                .Empty()
                .WithMessage("Collection Must Be Empty");
        }
    }
}
