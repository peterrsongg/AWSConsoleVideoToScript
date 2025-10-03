using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using FFMpegCore;
using FFMpegCore.Pipes;
using System;
using System.IO;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;
using SharedUtilityMethods;

namespace VideoScreenshotter
{
    public class VideoFrameExtractor
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _destinationBucket;
        private readonly ILambdaContext _context;

        public VideoFrameExtractor(IAmazonS3 s3Client, string destinationBucket, ILambdaContext context)
        {
            _s3Client = s3Client;
            _destinationBucket = destinationBucket;
            _context = context;
        }

        public async Task<ProcessVideoOutput> ProcessVideo(string sourceBucket, string videoKey)
        {
            var ext = Path.GetExtension(videoKey);
            var tmpPath = Path.Combine("/tmp", $"{Guid.NewGuid()}{ext}");
            
            try
            {
                // Download video to temp file
                using var response = await _s3Client.GetObjectAsync(sourceBucket, videoKey);
                using var fileStream = File.Create(tmpPath);
                await response.ResponseStream.CopyToAsync(fileStream);
                
                // Get video duration from file
                var videoInfo = await FFProbe.AnalyseAsync(tmpPath);
                var duration = videoInfo.Duration.TotalSeconds;
                // if splitting the video every 5 seconds yields greater than 20 parts, then
                // just split the video into 20 parts, else split it every 5 seconds
                var interval = duration / 5 > 20 ? duration / 20 : 5;
               
                var endIteration = duration / 5 > 20 ? 20 : duration / 5;
                var prefix = Path.GetFileNameWithoutExtension(videoKey);
                // Extract and upload 20 frames
                for (int i = 0; i < endIteration; i++)
                {
                    var frameTime = i * interval;
                    using var outputStream = new MemoryStream();

                    await FFMpegArguments
                        .FromFileInput(tmpPath)
                        .OutputToPipe(new StreamPipeSink(outputStream), options => options
                            .ForceFormat("image2pipe")
                            .WithVideoCodec("png")
                            .Seek(TimeSpan.FromSeconds(frameTime))
                            .WithFrameOutputCount(1))
                        .ProcessAsynchronously();

                    // Upload frame to destination bucket
                    var frameKey = $"{prefix}/frame_{i:D2}.png";
                    _context.Logger.LogInformation($"Uploading frame {frameKey} to bucket{_destinationBucket}");
                    await _s3Client.PutObjectAsync(new PutObjectRequest
                    {
                        BucketName = _destinationBucket,
                        Key = frameKey,
                        InputStream = new MemoryStream(outputStream.ToArray()),
                        ContentType = "image/png"
                    });
                }
                return new ProcessVideoOutput(_destinationBucket, prefix, endIteration);
            }
            finally
            {
                // Cleanup temp file
                if (File.Exists(tmpPath))
                    File.Delete(tmpPath);
            }
        }
    }
}