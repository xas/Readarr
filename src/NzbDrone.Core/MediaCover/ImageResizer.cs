using System.IO;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using SkiaSharp;

namespace NzbDrone.Core.MediaCover
{
    public interface IImageResizer
    {
        void Resize(string source, string destination, int height);
    }

    public class ImageResizer : IImageResizer
    {
        private const int JPEG_QUALITY = 92;
        private readonly IDiskProvider _diskProvider;
        private readonly bool _enabled;

        // Thumbnails don't need super high quality
        private readonly SKSamplingOptions _samplingOptions = new(SKFilterMode.Linear);

        public ImageResizer(IDiskProvider diskProvider, IPlatformInfo platformInfo)
        {
            _diskProvider = diskProvider;
            _enabled = true;
        }

        public void Resize(string source, string destination, int height)
        {
            if (!_enabled)
            {
                return;
            }

            try
            {
                using SKBitmap bitmap = SKBitmap.Decode(source);
                double scale = height / (double)bitmap.Height;
                SKImageInfo resizeInfo = new((int)(bitmap.Width * scale), height);
                using SKBitmap resizedSKBitmap = bitmap.Resize(resizeInfo, _samplingOptions);
                SKData data = resizedSKBitmap.Encode(SKEncodedImageFormat.Jpeg, JPEG_QUALITY);
                using FileStream fs = File.Create(destination);
                data.SaveTo(fs);
            }
            catch
            {
                if (_diskProvider.FileExists(destination))
                {
                    _diskProvider.DeleteFile(destination);
                }

                throw;
            }
        }
    }
}
