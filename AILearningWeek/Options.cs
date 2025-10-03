using Amazon.BedrockRuntime;
using Amazon.S3;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AILearningWeek
{
    public class Options
    {
        public IAmazonS3 S3Client { get; set; }
        public string BucketName { get; set; }
        public IAmazonBedrockRuntime BedrockRuntimeClient { get; set; }
        public string SaveFilePath { get; set; }

        public string Key { get; set; }

    }
}
