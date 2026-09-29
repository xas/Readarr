using NzbDrone.Common.Disk;

namespace NzbDrone.Core.Validation.Paths
{
    public class FileExistsValidator : PropertyValidator
    {
        private readonly IDiskProvider _diskProvider;

        public FileExistsValidator(IDiskProvider diskProvider)
        {
            _diskProvider = diskProvider;
        }

        protected override string GetDefaultMessageTemplate() => "File '{file}' does not exist";

        protected override bool IsValid(PropertyValidatorContext context)
        {
            context.MessageFormatter.AppendArgument("file", context.PropertyValue?.ToString() ?? string.Empty);

            if (context.PropertyValue == null)
            {
                return false;
            }

            return _diskProvider.FileExists(context.PropertyValue.ToString());
        }
    }
}
