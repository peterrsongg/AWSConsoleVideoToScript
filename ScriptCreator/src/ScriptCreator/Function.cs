using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Lambda.Core;
using Amazon.S3;
using SharedUtilityMethods;
using System.Text;
using Amazon.S3.Model;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ScriptCreator;

public class Function
{
    public Function()
    {
        S3Client = new AmazonS3Client();
        BedrockRuntimeClient = new AmazonBedrockRuntimeClient();
        _destinationBucket = Environment.GetEnvironmentVariable("DESTINATION_BUCKET");
        if (string.IsNullOrEmpty(_destinationBucket))
            throw new ArgumentNullException("Destination bucket must be set!");
    }


    readonly IAmazonS3 S3Client;
    readonly IAmazonBedrockRuntime BedrockRuntimeClient;
    readonly string _destinationBucket;
    /// <summary>
    /// A simple function that takes a string and does a ToUpper
    /// </summary>
    /// <param name="input">The event for the Lambda function handler to process.</param>
    /// <param name="context">The ILambdaContext that provides methods for logging and describing the Lambda environment.</param>
    /// <returns></returns>
    public async Task<ScriptCreatorOutput> FunctionHandler(ImageProcessorOutput input, ILambdaContext context)
    {
        context.Logger.LogInformation($"Retrieving description with file name {input.S3Key} from bucket {input.BucketName}");
        var getObjectResponse = await S3Client.GetObjectAsync(input.BucketName, input.S3Key).ConfigureAwait(false);
        context.Logger.LogInformation("Successfully retrieved object");
        var memoryStream = new MemoryStream();
        await getObjectResponse.ResponseStream.CopyToAsync(memoryStream).ConfigureAwait(false);
        memoryStream.Position = 0;

        string description = Encoding.UTF8.GetString(memoryStream.ToArray());

        //build the ConverseRequest
        List<Message> messageList = new List<Message>();
        List<ContentBlock> contentBlocks = new List<ContentBlock>();

        contentBlocks.Add(new ContentBlock
        {
            Text = description
        });
        contentBlocks.Add(new ContentBlock
        {
            Text = ScriptCreationPrompt2
        });

        messageList.Add(new Message
        {
            Content = contentBlocks,
            Role = ConversationRole.User
        });

        ConverseRequest converseRequest = new ConverseRequest
        {
            //arn:aws:bedrock:us-east-1:404186920727:inference-profile/global.anthropic.claude-sonnet-4-5-20250929-v1:0 
            // sonnet 4.5 im getting throttled, so i'll try sonnet 4 instead
            ModelId = "arn:aws:bedrock:us-east-1:404186920727:inference-profile/global.anthropic.claude-sonnet-4-20250514-v1:0",
            Messages = messageList,
            System = new List<SystemContentBlock> { new SystemContentBlock { Text = "You are a helpful assistant. You will be provided screenshots of the aws console" +
                "and it is your job to turn these descriptions into a script that uses the aws sdk for python. You are only responsible for creating the script. IF you are able to, " +
                "confirm that the script compiles against a python interpreter."} }
        };
        try
        {
            context.Logger.LogInformation("Sending converse request to bedrock");
            var response = await BedrockRuntimeClient.ConverseAsync(converseRequest);
            context.Logger.LogInformation("Successfully sent converse request to bedrock");
            string fileName = Utilities.CreateRandomFileName() + ".py";
            try
            {
                context.Logger.LogInformation("Saving results to s3");
                var putObjectResponse = await S3Client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = _destinationBucket,
                    Key = fileName,
                    ContentBody = response.Output.Message.Content.First().Text
                }).ConfigureAwait(false);
                context.Logger.LogInformation($"Successfully saved results to s3. Key: {fileName}. Bucket: {_destinationBucket}");
                return new ScriptCreatorOutput(_destinationBucket, fileName);
            }
            catch (Exception ex)
            {
                context.Logger.LogError(ex.Message);
                throw;
            }
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex.Message);
            throw;
        }
    }

    const string ScriptCreationPrompt1 = "You are reading a description of a series of screenshots from the AWS console. It is your job to turn these descriptions into a script that uses the aws" +
            "sdk for python, also known as boto3. The script should be written in a way that it can be executed by a python interpreter. " +
            "For the text you are analyzing, focus solely" +
            "on things that matter in terms of writing a script. Some examples of things that matter are the service the is user interacting with in the console, what action" +
            "the user is taking, what resource the user is acting on.";

    const string ScriptCreationPrompt2 = "You are reading a description of a series of screenshots from the AWS console. It is your job to turn these descriptions into a script that uses the aws" +
        "sdk for python, also known as boto3. The script should be written in a way where it can be executed by a python interpreter." +
        "If the description describes a user typing something into a text field and that value can be passed in as an argument, then make the script accept the value as an argument." +
        "For the text you are analyzing, focus soley on things that matter in terms of writing a script. Some examples of things that matter are the service the is user interacting with in the console, what action" +
        "the user is taking, what resource the user is acting on.";

}
