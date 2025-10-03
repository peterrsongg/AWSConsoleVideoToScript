using Amazon.BedrockRuntime.Model;
using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedUtilityMethods
{
    public class Utilities
    {
        //Save to local path specified in options and also uploads to s3
        public static async Task<string> SaveResults(IAmazonS3 s3Client, string content, string destinationBucket)
        {
            string fileName = CreateRandomFileName();
            try
            {
                var putObjectResponse = await s3Client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = destinationBucket,
                    Key = fileName,
                    ContentBody = content
                }).ConfigureAwait(false);
                return fileName;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                throw;
            }
        }
        public static string CreateRandomFileName()
        {
            Guid guid = Guid.NewGuid();
            long datetimeTicks = DateTime.Now.Ticks;
            return guid.ToString() + datetimeTicks;
        }
        public static void RemoveBackticks(string fullPath)
        {
            var lines = File.ReadAllLines(fullPath).Where(line => !line.StartsWith("```"));
            File.WriteAllLines(fullPath, lines);
        }
        public static string WriteToTmp(ConverseResponse response, string fileName)
        {
            string tempFilePath = Path.Combine("/tmp", fileName);
            using var fs = File.Create(tempFilePath);
            foreach (var message in response.Output.Message.Content)
            {
                fs.Write(Encoding.UTF8.GetBytes(message.Text));
            }
            fs.Flush();
            fs.Close();
            return tempFilePath;
        }
    }
}
