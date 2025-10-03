using Amazon.BedrockRuntime;
using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.BedrockRuntime.Model;
using Amazon.BedrockRuntime.Model.Internal.MarshallTransformations;
namespace AILearningWeek
{
    /// <summary>
    /// Processes the screenshots from s3 and uploads text description of the screenshots to s3 when finished
    /// </summary>
    public  class ImageProcessor
    {
        readonly IAmazonS3 _s3Client;
        readonly IAmazonBedrockRuntime _bedrockRuntimeClient;
        readonly Options _options;

        public ImageProcessor(Options options)
        {
            _s3Client = options.S3Client;
            _bedrockRuntimeClient = options.BedrockRuntimeClient;
            _options = options;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bucketName"></param>
        /// <param name="keys"></param>
        /// <returns>the name of the s3Key that contains the description of the processed images</returns>
        public async Task<string> ProcessS3ImagesAsync(string bucketName, List<string> keys)
        {
            List<Message> messageList = new List<Message>();
            List<ContentBlock> contentBlocks = new List<ContentBlock>();

            foreach (var key in keys)
            {
                var getObjectRequest = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = key
                };
                Console.WriteLine($"Retrieving image: {key}");
                using var getObjectResponse = await _s3Client.GetObjectAsync(getObjectRequest);
                Console.WriteLine($"Image {key} retrieved successfully");
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
                Console.WriteLine("Sending converse request to bedrock");
                var response = await _bedrockRuntimeClient.ConverseAsync(converseRequest);
                Console.WriteLine("Successfully sent converse request to bedrock");
                Console.WriteLine("Saving results to s3");
                string fileName = Helpers.CreateRandomFileName() + ".txt";
                string s3Key =  await Helpers.SaveResults(response, _options, fileName);
                Console.WriteLine($"Successfully saved results to s3: {s3Key}");
                return s3Key;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }

        public async Task<ConverseResponse> ProcessS3ImageAsync(string bucketName, string key)
        {
            var getObjectRequest = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = key
            };
            
            using var getObjectResponse = await _s3Client.GetObjectAsync(getObjectRequest);
            using var memoryStream = new MemoryStream();
            await getObjectResponse.ResponseStream.CopyToAsync(memoryStream);

            
            // Image is PNG format
            var imageFormat = ImageFormat.Png;

            var request = new ConverseRequest
            {
                Messages = new List<Message>
                {
                    new Message
                    {
                        Content = new List<ContentBlock>
                        {
                            new ContentBlock
                            {
                                Image = new ImageBlock
                                {
                                    Format = imageFormat,
                                    Source = new ImageSource
                                    {
                                        Bytes = memoryStream
                                    }
                                }
                            },
                            new ContentBlock
                            {
                                Text = "Describe this image in detail. For example, if the user has typed something into a text box, describe what the user has typed into the text box." +
                                "Also make not of what service is being used. For example, if the user is in the S3 console page then the user is using S3. If the user is in the EC2 console page" +
                                "then the user is in EC2."
                            }
                        },
                        Role = ConversationRole.User
                    }
                },
                ModelId = "arn:aws:bedrock:us-east-1:404186920727:inference-profile/global.anthropic.claude-sonnet-4-5-20250929-v1:0",
                System = new List<SystemContentBlock> { new SystemContentBlock { Text = "You are a helpful assistant. You will be provided screenshots of the aws console" +
                "and it is your job to describe in detail what you see in the image."} }
            };
            try
            {
                var response = await _bedrockRuntimeClient.ConverseAsync(request);
                await Helpers.SaveResults(response, _options, ".txt");
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }


    }
}
