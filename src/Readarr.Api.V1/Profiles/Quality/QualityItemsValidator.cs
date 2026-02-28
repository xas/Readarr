using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using FluentValidation.Validators;
using NzbDrone.Common.Extensions;

namespace Readarr.Api.V1.Profiles.Quality
{
    public static class QualityItemsValidator
    {
        public static IRuleBuilderOptions<T, IList<QualityProfileQualityItemResource>> ValidItems<T>(this IRuleBuilder<T, IList<QualityProfileQualityItemResource>> ruleBuilder)
        {
            ruleBuilder.SetValidator(new NotEmptyValidator<T, IList<QualityProfileQualityItemResource>>());
            ruleBuilder.SetValidator(new AllowedValidator<IList<QualityProfileQualityItemResource>>());
            ruleBuilder.SetValidator(new QualityNameValidator<IList<QualityProfileQualityItemResource>>());
            ruleBuilder.SetValidator(new GroupItemValidator<IList<QualityProfileQualityItemResource>>());
            ruleBuilder.SetValidator(new ItemGroupIdValidator<IList<QualityProfileQualityItemResource>>());
            ruleBuilder.SetValidator(new UniqueIdValidator<IList<QualityProfileQualityItemResource>>());
            ruleBuilder.SetValidator(new UniqueQualityIdValidator<IList<QualityProfileQualityItemResource>>());
            ruleBuilder.SetValidator(new AllQualitiesValidator<IList<QualityProfileQualityItemResource>>());

            return ruleBuilder.SetValidator(new ItemGroupNameValidator<IList<QualityProfileQualityItemResource>>());
        }
    }

    public class AllowedValidator<T> : AbstractValidator<T>
    {
        public AllowedValidator()
        {
            RuleFor(q => q)
                .Custom((q, ctx) =>
                {
                    bool ok = q is IList<QualityProfileQualityItemResource> list && list.Any(l => l.Allowed);
                    if (!ok)
                    {
                        ctx.AddFailure("Must contain at least one allowed quality");
                    }
                });
        }
    }

    public class GroupItemValidator<T> : AbstractValidator<T>
    {
        public GroupItemValidator()
        {
            RuleFor(g => g)
                .Custom((g, ctx) =>
                {
                    bool ok = g is IList<QualityProfileQualityItemResource> list && !list.Any(l => l.Name.IsNotNullOrWhiteSpace() && l.Items.Count <= 1);
                    if (!ok)
                    {
                        ctx.AddFailure("Groups must contain multiple qualities");
                    }
                });
        }
    }

    public class QualityNameValidator<T> : AbstractValidator<T>
    {
        public QualityNameValidator()
        {
            RuleFor(q => q)
                .Custom((q, ctx) =>
                {
                    bool ok = q is IList<QualityProfileQualityItemResource> list && !list.Any(l => l.Name.IsNotNullOrWhiteSpace() && l.Quality is not null);
                    if (!ok)
                    {
                        ctx.AddFailure("Individual qualities should not be named");
                    }
                });
        }
    }

    public class ItemGroupNameValidator<T> : AbstractValidator<T>
    {
        public ItemGroupNameValidator()
        {
            RuleFor(q => q)
                .Custom((q, ctx) =>
                {
                    bool ok = q is IList<QualityProfileQualityItemResource> list && !list.Any(l => l.Quality is null && l.Name.IsNotNullOrWhiteSpace());
                    if (!ok)
                    {
                        ctx.AddFailure("Groups must have a name");
                    }
                });
        }
    }

    public class ItemGroupIdValidator<T> : AbstractValidator<T>
    {
        public ItemGroupIdValidator()
        {
            RuleFor(item => item)
                .Custom((item, ctx) =>
                {
                    bool ok = item is IList<QualityProfileQualityItemResource> list && !list.Any(l => l.Quality is null && l.Id == 0);
                    if (!ok)
                    {
                        ctx.AddFailure("Groups must have an ID");
                    }
                });
        }
    }

    public class UniqueIdValidator<T> : AbstractValidator<T>
    {
        public UniqueIdValidator()
        {
            RuleFor(q => q)
                .Custom((q, ctx) =>
                {
                    if (q is IList<QualityProfileQualityItemResource> list)
                    {
                        var ids = list.Where(i => i.Id > 0).Select(i => i.Id);
                        var groupedIds = ids.GroupBy(i => i);
                        if (groupedIds.All(g => g.Count() == 1))
                        {
                            return;
                        }
                    }

                    ctx.AddFailure("Groups must have an unique ID");
                });
        }
    }

    public class UniqueQualityIdValidator<T> : AbstractValidator<T>
    {
        public UniqueQualityIdValidator()
        {
            RuleFor(q => q)
                .Custom((q, ctx) =>
                {
                    if (q is IList<QualityProfileQualityItemResource> list)
                    {
                        var qualityIds = new HashSet<int>();
                        foreach (var item in list)
                        {
                            if (item.Id > 0)
                            {
                                foreach (var quality in item.Items)
                                {
                                    if (qualityIds.Contains(quality.Quality.Id))
                                    {
                                        ctx.AddFailure("Qualities can only be used once");
                                        return;
                                    }

                                    qualityIds.Add(quality.Quality.Id);
                                }
                            }
                            else
                            {
                                if (qualityIds.Contains(item.Quality.Id))
                                {
                                    ctx.AddFailure("Qualities can only be used once");
                                    return;
                                }

                                qualityIds.Add(item.Quality.Id);
                            }
                        }

                        return;
                    }

                    ctx.AddFailure("Qualities can only be used once");
                });
        }
    }

    public class AllQualitiesValidator<T> : AbstractValidator<T>
    {
        public AllQualitiesValidator()
        {
            RuleFor(q => q)
                .Custom((q, ctx) =>
                {
                    if (q is IList<QualityProfileQualityItemResource> list)
                    {
                        var qualityIds = new HashSet<int>();
                        foreach (var item in list)
                        {
                            if (item.Id > 0)
                            {
                                foreach (var quality in item.Items)
                                {
                                    qualityIds.Add(quality.Quality.Id);
                                }
                            }
                            else
                            {
                                qualityIds.Add(item.Quality.Id);
                            }
                        }

                        var allQualityIds = NzbDrone.Core.Qualities.Quality.All;

                        foreach (var quality in allQualityIds)
                        {
                            if (!qualityIds.Contains(quality.Id))
                            {
                                ctx.AddFailure("Must contain all qualities");
                                return;
                            }
                        }

                        return;
                    }

                    ctx.AddFailure("Must contain all qualities");
                });
        }
    }
}
