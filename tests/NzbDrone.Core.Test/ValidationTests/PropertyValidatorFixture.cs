using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Validation;
using NzbDrone.Core.Validation.Paths;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.ValidationTests
{
    [TestFixture]
    public class PropertyValidatorFixture
    {
        private class ParentCapturingValidator : PropertyValidator
        {
            public object Parent { get; private set; }

            protected override string GetDefaultMessageTemplate() => "Parent is {name}";

            protected override bool IsValid(PropertyValidatorContext context)
            {
                Parent = context.ParentContext.InstanceToValidate;
                context.MessageFormatter.AppendArgument("name", ((Author)Parent).Name);

                return false;
            }
        }

        [Test]
        public void should_run_on_null_value()
        {
            var validator = new TestValidator<Author>
                            {
                                v => v.RuleFor(s => s.Path).IsValidPath()
                            };

            var result = validator.Validate(new Author { Path = null });

            result.IsValid.Should().BeFalse();
            result.Errors.Single().ErrorMessage.Should().Be("Invalid Path: ''");
        }

        [Test]
        public void should_format_message_with_appended_arguments()
        {
            var validator = new TestValidator<Author>
                            {
                                v => v.RuleFor(s => s.Path).IsValidPath()
                            };

            var result = validator.Validate(new Author { Path = "relative/path" });

            result.IsValid.Should().BeFalse();
            result.Errors.Single().ErrorMessage.Should().Be("Invalid Path: 'relative/path'");
            result.Errors.Single().PropertyName.Should().Be("Path");
        }

        [Test]
        public void should_be_valid_for_valid_path()
        {
            var validator = new TestValidator<Author>
                            {
                                v => v.RuleFor(s => s.Path).IsValidPath()
                            };

            validator.Validate(new Author { Path = @"C:\Test\Books\Author".AsOsAgnostic() }).IsValid.Should().BeTrue();
        }

        [Test]
        public void should_give_access_to_the_parent_object()
        {
            var subject = new ParentCapturingValidator();
            var author = new Author { Path = "any", Metadata = new AuthorMetadata { Name = "Author Name" } };

            var validator = new TestValidator<Author>
                            {
                                v => v.RuleFor(s => s.Path).SetValidator(subject)
                            };

            var result = validator.Validate(author);

            subject.Parent.Should().BeSameAs(author);
            result.Errors.Single().ErrorMessage.Should().Be("Parent is Author Name");
        }
    }
}
