using FluentValidation;
using NzbDrone.Core.Download;

namespace NzbDrone.Core.Validation
{
    public class DownloadClientExistsValidator : AbstractValidator<int>
    {
        private readonly IDownloadClientFactory _downloadClientFactory;

        public DownloadClientExistsValidator(IDownloadClientFactory downloadClientFactory)
        {
            _downloadClientFactory = downloadClientFactory;

            RuleFor(dc => dc)
                .Must(dc => _downloadClientFactory.Exists(dc))
                .When(dc => dc != 0)
                .WithMessage("Download Client does not exist");
        }
    }
}
