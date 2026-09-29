using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Validation.Paths;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.ValidationTests
{
    public class AuthorPathValidatorFixture : CoreTest<AuthorPathValidator>
    {
        private Author _author;

        [SetUp]
        public void Setup()
        {
            _author = Builder<Author>.CreateNew()
                                     .With(s => s.Id = 1)
                                     .With(s => s.Path = @"C:\Test\Books\Author".AsOsAgnostic())
                                     .Build();
        }

        private void GivenExistingAuthorPath(int id, string path)
        {
            Mocker.GetMock<IAuthorService>()
                  .Setup(s => s.AllAuthorPaths())
                  .Returns(new Dictionary<int, string> { { id, path } });
        }

        private TestValidator<Author> ExcludingCurrentAuthor()
        {
            return new TestValidator<Author>
                   {
                       v => v.RuleFor(s => s.Path).SetValidator(Subject, s => s.Id)
                   };
        }

        [Test]
        public void should_be_valid_if_path_is_not_used()
        {
            GivenExistingAuthorPath(2, @"C:\Test\Books\Other".AsOsAgnostic());

            ExcludingCurrentAuthor().Validate(_author).IsValid.Should().BeTrue();
        }

        [Test]
        public void should_be_valid_if_path_is_used_by_the_same_author()
        {
            GivenExistingAuthorPath(1, _author.Path);

            ExcludingCurrentAuthor().Validate(_author).IsValid.Should().BeTrue();
        }

        [Test]
        public void should_not_be_valid_if_path_is_used_by_another_author()
        {
            GivenExistingAuthorPath(2, _author.Path);

            var result = ExcludingCurrentAuthor().Validate(_author);

            result.IsValid.Should().BeFalse();
            result.Errors.Single().ErrorMessage.Should().Be($"Path '{_author.Path}' is already configured for another author");
        }

        [Test]
        public void should_not_be_valid_if_path_is_used_and_no_author_is_excluded()
        {
            GivenExistingAuthorPath(1, _author.Path);

            var validator = new TestValidator<Author>
                            {
                                v => v.RuleFor(s => s.Path).SetValidator(Subject)
                            };

            validator.Validate(_author).IsValid.Should().BeFalse();
        }

        [Test]
        public void should_be_valid_if_path_is_null()
        {
            GivenExistingAuthorPath(2, _author.Path);
            _author.Path = null;

            ExcludingCurrentAuthor().Validate(_author).IsValid.Should().BeTrue();
        }
    }
}
