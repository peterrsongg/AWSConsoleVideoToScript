using Amazon.Lambda.Core;
using Amazon.BedrockRuntime.Model;
using Amazon.BedrockRuntime;
using Amazon.S3.Model;
using Amazon.S3;
using SharedUtilityMethods;
// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ImageProcessor;

public class Function
{
    private IAmazonBedrockRuntime BedrockRuntimeClient { get; set; }
    private IAmazonS3 S3Client { get; set; }
    private readonly string _destinationBucket;
    public Function()
    {
        BedrockRuntimeClient = new AmazonBedrockRuntimeClient();
        S3Client = new AmazonS3Client();
        _destinationBucket = Environment.GetEnvironmentVariable("DESTINATION_BUCKET");
        if (string.IsNullOrEmpty(_destinationBucket))
            throw new ArgumentNullException("Destination bucket must be set!");
    }
    
    /// <summary>
    /// A simple function that takes a string and does a ToUpper
    /// </summary>
    /// <param name="input">The event for the Lambda function handler to process.</param>
    /// <param name="context">The ILambdaContext that provides methods for logging and describing the Lambda environment.</param>
    /// <returns></returns>
    public async Task<ImageProcessorOutput> FunctionHandler(ProcessVideoOutput input, ILambdaContext context)
    {
        List<Message> messageList = new List<Message>();
        List<ContentBlock> contentBlocks = new List<ContentBlock>();
        string bucketName = input.BucketName;
        try
        {
            var listObjectsResponse = await S3Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = bucketName,
                Prefix = input.Prefix,
            }).ConfigureAwait(false);

            foreach(var s3Object in listObjectsResponse.S3Objects)
            {
                var getObjectRequest = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = s3Object.Key,
                };
                context.Logger.LogInformation($"getting object: {s3Object.Key} from bucket: {bucketName}");
                using var getObjectResponse = await S3Client.GetObjectAsync(getObjectRequest).ConfigureAwait(false);
                context.Logger.LogInformation($"successfully retrieved object {s3Object.Key} from bucket: {bucketName}");
                using var memoryStream = new MemoryStream();
                await getObjectResponse.ResponseStream.CopyToAsync(memoryStream);
                contentBlocks.Add(new ContentBlock
                {
                    Image = new ImageBlock
                    {
                        Format = ImageFormat.Png,
                        Source = new ImageSource
                        {
                            Bytes = memoryStream
                        }
                    }
                });
            }
            contentBlocks.Add(new ContentBlock
            {
                Text = "Describe this image in detail. For example, if the user has typed something into a text box, describe what the user has typed into the text box." +
                    "Also make note of what service is being used. For example, if the user is in the S3 console page then the user is using S3. If the user is in the EC2 console page" +
                    "then the user is in EC2. Your descriptions will later be used to formulate an aws script, so keep that in mind."
            });
            messageList.Add(new Message
            {
                Content = contentBlocks,
                Role = ConversationRole.User
            });
            var converseRequest = new ConverseRequest
            {
                Messages = messageList,
                ModelId = "arn:aws:bedrock:us-east-1:404186920727:inference-profile/global.anthropic.claude-sonnet-4-5-20250929-v1:0",
                System = new List<SystemContentBlock> { new SystemContentBlock { Text = "You are a helpful assistant. You will be provided screenshots of the aws console" +
                "and it is your job to describe in detail what you see in the image. your descriptions will later be used to formulate an aws script, so keep that in mind."} }
            };
            try
            {
                context.Logger.LogInformation("Sending converse request to bedrock");
                var response = await BedrockRuntimeClient.ConverseAsync(converseRequest).ConfigureAwait(false);
                context.Logger.LogInformation("Successfully sent converse request to bedrock");
                string fileName = await Utilities.SaveResults(S3Client, response.Output.Message.Content.First().Text, _destinationBucket);
                context.Logger.LogInformation($"Successfully saved results to S3 at bucket: {_destinationBucket} with key {fileName}");
                return new ImageProcessorOutput(_destinationBucket, fileName);
            }
            catch (Exception ex)
            {
                context.Logger.LogError($"Error sending converse request to bedrock, {ex.Message}");
                throw;
            }
        }
        catch(Exception ex)
        {
            context.Logger.LogError($"Error processing input, {ex.Message}");
            throw;
        }
    }
}
