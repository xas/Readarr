using System;
using System.IO;
using FluentValidation;
using FluentValidation.Validators;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Organizer;

namespace Readarr.Api.V1.Author
{
    public class AuthorFolderAsRootFolderValidator<T> : PropertyValidator<T, string>
    {
        private readonly IBuildFileNames _fileNameBuilder;

        public override string Name => "AuthorFolderAsRootFolderValidator";

        public AuthorFolderAsRootFolderValidator(IBuildFileNames fileNameBuilder)
        {
            _fileNameBuilder = fileNameBuilder;
        }

        public override bool IsValid(ValidationContext<T> context, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            if (context.InstanceToValidate is not AuthorResource authorResource)
            {
                return true;
            }

            var rootFolder = new DirectoryInfo(value).Name;
            var author = authorResource.ToModel();
            var authorFolder = _fileNameBuilder.GetAuthorFolder(author);

            context.MessageFormatter.AppendArgument("authorFolder", authorFolder);
            if (authorFolder == rootFolder)
            {
                return false;
            }

            var distance = authorFolder.LevenshteinDistance(rootFolder);
            return distance >= Math.Max(1, authorFolder.Length * 0.2);
        }

        protected override string GetDefaultMessageTemplate(string errorCode) => "{errorCode}: Root folder path '{PropertyValue}' contains author folder '{authorFolder}'";
    }
}
