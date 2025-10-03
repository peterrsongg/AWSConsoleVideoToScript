using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AILearningWeek
{
    public class ScriptCleaner
    {
        readonly IAmazonS3 _s3Client;
        readonly IAmazonBedrockRuntime _bedrockRuntimeClient;
        readonly string _saveFilePath;
        readonly string bucketName;
        readonly Options _options;
        public ScriptCleaner(Options options) 
        {
            _s3Client = options.S3Client;
            _bedrockRuntimeClient = options.BedrockRuntimeClient;
            _saveFilePath = options.SaveFilePath;
            bucketName = options.BucketName;
            _options = options;
        }

        public async Task<string> CleanScriptAsync(string key)
        {
            Console.WriteLine($"Retriving description with file name {key} from s3 bucket: {_options.BucketName}");
            var getObjectRequest = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = key
            };
            using var getObjectResponse = await _s3Client.GetObjectAsync(getObjectRequest);
            Console.WriteLine("Successfully retrieved script");
            using var memoryStream = new MemoryStream();
            await getObjectResponse.ResponseStream.CopyToAsync(memoryStream);

            //build the ConverseRequest
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
                "Also, do not modify the existing code. Only remove the parts that aren't code so that the script can run on its own. ANY line that starts with three backticks MUST be removed."
            });

            messageList.Add(new Message
            {
                Content = contentBlocks,
                Role = ConversationRole.User
            });

            ConverseRequest converseRequest = new ConverseRequest
            {
                ModelId = "arn:aws:bedrock:us-east-1:404186920727:inference-profile/us.anthropic.claude-3-7-sonnet-20250219-v1:0",
                Messages = messageList,
                System = new List<SystemContentBlock> { new SystemContentBlock { Text = "Make sure that the finalized script that comes from the script cleaning only contains code. " +
                "Comments within the script are fine but there should be nothing outside of comments and code in the final file."} }
            };

            try
            {
                Console.WriteLine("Sending converse request to bedrock to create script");
                var response = await _bedrockRuntimeClient.ConverseAsync(converseRequest);
                Console.WriteLine("Successfully cleaned script");
                string ext = Path.GetExtension(key);
                string newFileName = Path.GetFileNameWithoutExtension(key) + "cleaned" + ext;
                string scriptKey = await Helpers.SaveResults(response, _options, newFileName);
                Console.WriteLine($"Successfully created cleaned script with name {scriptKey} in s3 bucket {_options.BucketName}");
                return scriptKey;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }
    }
}
