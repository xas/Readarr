using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Profiles.Delay
{
    public class DelayProfileTagInUseValidator : AbstractValidator<HashSet<int>>
    {
        private readonly IDelayProfileService _delayProfileService;

        public DelayProfileTagInUseValidator(IDelayProfileService delayProfileService)
        {
            _delayProfileService = delayProfileService;

            RuleFor(hash => hash)
                .Custom((hash, ctx) =>
                {
                    if (hash is null)
                    {
                        return;
                    }

                    if (hash.Empty())
                    {
                        return;
                    }

                    // TODO : Verify the code
                    bool noTag = _delayProfileService.All().None(d => d.Tags.Intersect(hash).Any());
                    if (!noTag)
                    {
                        ctx.AddFailure("One or more tags is used in another profile");
                    }
                });
        }
    }
}
