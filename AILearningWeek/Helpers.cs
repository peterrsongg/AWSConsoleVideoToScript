using Amazon.BedrockRuntime.Model;
using Amazon.S3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Amazon.S3.Model;


namespace AILearningWeek
{
    static class Helpers
    {
        //Save to local path specified in options and also uploads to s3
        public static async Task<string> SaveResults(ConverseResponse response, Options options, string fileName)
        {
            string fullPath = Path.Join(options.SaveFilePath, fileName);
            using var fs = File.OpenWrite(fullPath);
            foreach (var message in response.Output.Message.Content)
            {
                fs.Write(Encoding.UTF8.GetBytes(message.Text));
            }
            fs.Flush();
            fs.Close();
            RemoveBackticks(fullPath);
            try
            {
                var putObjectResponse = await options.S3Client.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = options.BucketName,
                    Key = fileName,
                    FilePath = fullPath
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
    }
}
