using FFMpegCore;

namespace VideoScreenshotter
{
    public static class FFmpegConfig
    {
        public static void Configure()
        {
            // Set FFmpeg binary paths for packaged binaries
            var binaryPath = Path.Combine(AppContext.BaseDirectory, "ffmpeg");
            GlobalFFOptions.Configure(new FFOptions
            {
                BinaryFolder = binaryPath,
                TemporaryFilesFolder = "/tmp"
            });
        }
    }
}