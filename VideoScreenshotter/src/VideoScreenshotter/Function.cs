using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Model;
using FFMpegCore;
using FFMpegCore.Pipes;
using System.Text.Json.Serialization;
using SharedUtilityMethods;
// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace VideoScreenshotter;

public class Function
{
    private IAmazonS3 S3Client { get; set; }
    private readonly string _destinationBucket;

    public Function()
    {
        FFmpegConfig.Configure();
        S3Client = new AmazonS3Client();
        _destinationBucket = Environment.GetEnvironmentVariable("DESTINATION_BUCKET");
        if (string.IsNullOrEmpty(_destinationBucket))
            throw new ArgumentNullException("Destination bucket must be set!");
    }

    public Function(IAmazonS3 s3Client)
    {
        FFmpegConfig.Configure();
        this.S3Client = s3Client;
    }
    public record S3BucketInfo(
        [property: JsonPropertyName("name")] string Name
    );

    public record S3ObjectInfo(
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("size")] long? Size,
        [property: JsonPropertyName("etag")] string? ETag,
        [property: JsonPropertyName("version-id")] string? VersionId,
        [property: JsonPropertyName("sequencer")] string? Sequencer
    );

    public record S3Detail(
        [property: JsonPropertyName("bucket")] S3BucketInfo Bucket,
        [property: JsonPropertyName("object")] S3ObjectInfo Object,
        [property: JsonPropertyName("reason")] string? Reason
    );

    public record EventBridgeEvent(
        [property: JsonPropertyName("detail")] S3Detail Detail
    );
    /// <summary>
    /// this one works with event bridge
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task<ProcessVideoOutput> FunctionHandler(EventBridgeEvent input, ILambdaContext context)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (input.Detail == null) throw new ArgumentNullException(nameof(input.Detail));
        if (input.Detail.Bucket == null) throw new ArgumentNullException(nameof(input.Detail.Bucket));
        if (input.Detail.Object == null) throw new ArgumentNullException(nameof(input.Detail.Object));

        var extractor = new VideoFrameExtractor(S3Client, _destinationBucket, context);
        context.Logger.LogInformation($"Processing video: {input.Detail.Object.Key}");
        var output = await extractor.ProcessVideo(input.Detail.Bucket.Name, input.Detail.Object.Key);
        context.Logger.LogInformation($"Successfully extracted {output.NumberOfFramesExtracted} frames from {input.Detail.Object.Key}");
        return output;
    }


    //this version responds to S3 Event from lambda
    /// <summary>
    /// This method is called for every Lambda invocation. This method takes in an S3 event object and can be used 
    /// to respond to S3 notifications.
    /// </summary>
    /// <param name="evnt">The event for the Lambda function handler to process.</param>
    /// <param name="context">The ILambdaContext that provides methods for logging and describing the Lambda environment.</param>
    /// <returns></returns>
    //public async Task<ProcessVideoOutput> FunctionHandler(S3Event evnt, ILambdaContext context)
    //{
    //    var extractor = new VideoFrameExtractor(S3Client, _destinationBucket);
    //    foreach (var record in evnt.Records ?? new List<S3Event.S3EventNotificationRecord>())
    //    {
    //        var s3Event = record.S3;
    //        if (s3Event == null) continue;

    //        try
    //        {
    //            context.Logger.LogInformation($"Processing video: {s3Event.Object.Key}");
    //            var output = await extractor.ProcessVideo(s3Event.Bucket.Name, s3Event.Object.Key);
    //            context.Logger.LogInformation($"Successfully extracted {output.NumberOfFramesExtracted} frames from {s3Event.Object.Key}");
    //            return output;
    //        }
    //        catch (Exception e)
    //        {
    //            context.Logger.LogError($"Error processing {s3Event.Object.Key}: {e.Message}");
    //            throw;
    //        }
    //    }
    //    //return an empty object
    //    return new ProcessVideoOutput(BucketName: "", "", 0);
    //}
}
