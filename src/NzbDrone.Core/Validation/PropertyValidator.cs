using FluentValidation;
using FluentValidation.Internal;

namespace NzbDrone.Core.Validation
{
    /// <summary>
    /// FluentValidation 9 style property validator, kept so the validators work unchanged on FluentValidation 12.
    /// Attach it with <c>RuleFor(...).SetValidator(validator)</c> (see <see cref="PropertyValidatorExtensions"/>).
    /// Unlike a child <c>AbstractValidator&lt;TProperty&gt;</c>, it also runs on <c>null</c> values and can read the parent object.
    /// </summary>
    public abstract class PropertyValidator
    {
        public virtual string Name => GetType().Name;

        protected abstract string GetDefaultMessageTemplate();

        protected abstract bool IsValid(PropertyValidatorContext context);

        internal string DefaultMessageTemplate => GetDefaultMessageTemplate();

        internal bool Validate(PropertyValidatorContext context) => IsValid(context);
    }

    public class PropertyValidatorContext
    {
        public PropertyValidatorContext(IValidationContext parentContext, MessageFormatter messageFormatter, object propertyValue)
        {
            ParentContext = parentContext;
            MessageFormatter = messageFormatter;
            PropertyValue = propertyValue;
        }

        /// <summary>Validation context of the object that owns the property (<c>ParentContext.InstanceToValidate</c>).</summary>
        public IValidationContext ParentContext { get; }

        public MessageFormatter MessageFormatter { get; }

        public object PropertyValue { get; }
    }

    public class PropertyValidatorAdapter<T, TProperty> : FluentValidation.Validators.PropertyValidator<T, TProperty>
    {
        private readonly PropertyValidator _validator;

        public PropertyValidatorAdapter(PropertyValidator validator)
        {
            _validator = validator;
        }

        public override string Name => _validator.Name;

        protected override string GetDefaultMessageTemplate(string errorCode) => _validator.DefaultMessageTemplate;

        public override bool IsValid(ValidationContext<T> context, TProperty value)
        {
            return _validator.Validate(new PropertyValidatorContext(context, context.MessageFormatter, value));
        }
    }

    public static class PropertyValidatorExtensions
    {
        public static IRuleBuilderOptions<T, TProperty> SetValidator<T, TProperty>(this IRuleBuilder<T, TProperty> ruleBuilder, PropertyValidator validator)
        {
            return ruleBuilder.SetValidator(new PropertyValidatorAdapter<T, TProperty>(validator));
        }
    }
}
