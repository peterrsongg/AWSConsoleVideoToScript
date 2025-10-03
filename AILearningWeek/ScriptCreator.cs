using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.S3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon.S3.Model;

namespace AILearningWeek
{
    /// <summary>
    /// Reads from the text files and tries to create a script.
    /// </summary>
    public class ScriptCreator
    {
        readonly IAmazonS3 _s3Client;
        readonly IAmazonBedrockRuntime _bedrockRuntimeClient;
        private readonly Options _options;

        public ScriptCreator(Options options)
        {
            _s3Client = options.S3Client;
           _bedrockRuntimeClient = options.BedrockRuntimeClient;
            _options = options;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="s3Key">The key to read the description from</param>
        /// <returns></returns>
        public async Task<string> CreateScriptAsync(string s3Key)
        {
            //get the object
            Console.WriteLine($"Retriving description with file name {s3Key} from s3 bucket: {_options.BucketName}");
            var getObjectResponse = await _s3Client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _options.BucketName,
                Key = s3Key
            }).ConfigureAwait(false);
            Console.WriteLine("Successfully retrieved description");
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
                Text = "You are reading a description of a series of screenshots from the AWS console. It is your job to turn these descriptions into a script that uses the aws" +
                "sdk for python, also know as boto3. The script should be written in a way that it can be executed by a python interpreter. For the text you are analyzing, focus solely" +
                "on things that matter in terms of writing a script. Some examples of things that matter are the service the is user interacting with in the console, what action" +
                "the user is taking, what resource the user is acting on."
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
                System = new List<SystemContentBlock> { new SystemContentBlock { Text = "You are a helpful assistant. You will be provided screenshots of the aws console" +
                "and it is your job to turn these descriptions into a script that uses the aws sdk for python. You are only responsible for creating the script. IF you are able to, " +
                "confirm that the script compiles against a python interpreter."} }
            };
            try
            {
                Console.WriteLine("Sending converse request to bedrock to create script");
                var response = await _bedrockRuntimeClient.ConverseAsync(converseRequest);
                Console.WriteLine("Successfully created script");
                var fileName = Helpers.CreateRandomFileName() + ".py";
                string scriptKey = await Helpers.SaveResults(response, _options, fileName);
                Console.WriteLine($"Successfully create script with name {scriptKey} in s3 bucket {_options.BucketName}");
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
