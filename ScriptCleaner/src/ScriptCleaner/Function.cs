using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using SharedUtilityMethods;
using System.Text;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ScriptCleaner;

public class Function
{
    readonly IAmazonS3 S3Client;
    readonly IAmazonBedrockRuntime BedrockRuntimeClient;
    readonly string _destinationBucket;

    public Function()
    {
        S3Client = new AmazonS3Client();
        BedrockRuntimeClient = new AmazonBedrockRuntimeClient();
        _destinationBucket = Environment.GetEnvironmentVariable("DESTINATION_BUCKET");
        if (string.IsNullOrEmpty(_destinationBucket))
            throw new ArgumentNullException("Destination bucket environment variable must be set");
    }

    /// <summary>
    /// A simple function that takes a string and does a ToUpper
    /// </summary>
    /// <param name="input">The event for the Lambda function handler to process.</param>
    /// <param name="context">The ILambdaContext that provides methods for logging and describing the Lambda environment.</param>
    /// <returns></returns>
    public async Task<ScriptCleanerOutput> FunctionHandler(ScriptCreatorOutput input, ILambdaContext context)
    {
        context.Logger.LogInformation($"Retreiving script {input.S3Key} from {input.BucketName}");
        var getObjectRequest = new GetObjectRequest
        {
            BucketName = input.BucketName,
            Key = input.S3Key
        };
        using var getObjectResponse = await S3Client.GetObjectAsync(getObjectRequest);
        using var memoryStream = new MemoryStream();
        await getObjectResponse.ResponseStream.CopyToAsync(memoryStream);

        List<Message> messageList = new List<Message>();
        List<ContentBlock> contentBlocks = new List<ContentBlock>();
        string script = Encoding.UTF8.GetString(memoryStream.ToArray());


        contentBlocks.Add(new ContentBlock
        {
            Text = script
        });

        contentBlocks.Add(new ContentBlock
        {
            Text = "Your job is to clean up the script above. Remove any text that isn't related to the script such as an explanation of what the script does," +
            "what language the script is written in (usually this is three backticks followed by the name of the language. The output should only be working code." +
            "DO NOT modify the existing code. Only remove the parts that aren't code so that the script can run on its own. ANY line that starts with three backticks MUST be removed."
        });

        messageList.Add(new Message
        {
            Content = contentBlocks,
            Role = ConversationRole.User
        });

        ConverseRequest converseRequest = new ConverseRequest
        {
            ModelId = "arn:aws:bedrock:us-east-1:404186920727:inference-profile/global.anthropic.claude-sonnet-4-20250514-v1:0",
            Messages = messageList,
            System = new List<SystemContentBlock> { new SystemContentBlock { Text = "Make sure that the finalized script that comes from the script cleaning only contains code. " +
                "Comments within the script are fine but there should be nothing outside of comments and code in the final file."} }
        };
        try
        {
            context.Logger.LogInformation("Sending converse request to bedrock to create script.");
            var response = await BedrockRuntimeClient.ConverseAsync(converseRequest);
            context.Logger.LogInformation("Successfully sent converse request sent to bedrock to create script.");
            //write the output of the response to a temp file
            var tempFilePath = Utilities.WriteToTmp(response, input.S3Key);
            //clean the backticks
            Utilities.RemoveBackticks(tempFilePath);
            //upload that to s3
            var fileName = Path.GetFileNameWithoutExtension(input.S3Key);
            var finalFileName = fileName + "-cleaned" + Path.GetExtension(input.S3Key);
            var putObjectRequest = new PutObjectRequest
            {
                BucketName = _destinationBucket,
                Key = finalFileName,
                FilePath = tempFilePath
            };
            await S3Client.PutObjectAsync(putObjectRequest).ConfigureAwait(false);
            context.Logger.LogInformation($"Successfully uploaded cleaned script to {_destinationBucket} with key: {finalFileName}");
            return new ScriptCleanerOutput(_destinationBucket, finalFileName);
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex.Message);
            throw;
        }
    }
}
