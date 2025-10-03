using AILearningWeek;
using Amazon;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.S3;
using Amazon.S3.Model;
using System.ComponentModel.Design;


string s3BucketName = "testing-my-s3-video-bucket-destination";
string outputFolder = @"C:\Users\petesong\Desktop\projects\AILearningWeek\ScreenshotDescriptionResults";
using var s3Client = new AmazonS3Client(RegionEndpoint.USEast1);
var listObjectsResponse = await s3Client.ListObjectsAsync(new ListObjectsRequest
{
    BucketName = s3BucketName,
    Prefix = "console-screen-recording-2"

}).ConfigureAwait(false);
List<string> listObjects = new List<string>();
foreach (var s3Object in listObjectsResponse.S3Objects)
{
    listObjects.Add(s3Object.Key);
}
using var bedrockRuntimeClient = new AmazonBedrockRuntimeClient(RegionEndpoint.USEast1);

//var imageProcessor = new ImageProcessor(s3Client, bedrockRuntimeClient,outputFolder);
//var converseResponse = await videoProcessor.ProcessS3ImageAsync(s3BucketName, s3Key);
//var converseResponse2 = await imageProcessor.ProcessS3ImagesAsync(s3BucketName, s3Keys);
Options options = new Options
{
    BedrockRuntimeClient = bedrockRuntimeClient,
    S3Client = s3Client,
    SaveFilePath = outputFolder,
    BucketName = s3BucketName,
};

var imageProcessor = new ImageProcessor(options);
string s3Key = await imageProcessor.ProcessS3ImagesAsync(s3BucketName, listObjects);
string hardcodedKey = "d3b611eb-3e7b-4701-9aae-df21f104d7c4638949222318445772.txt";
string hardcodedScriptName = "65829e50-56e7-4d64-8c82-f2007736922e638949226020750412.py";
var scriptCreator = new ScriptCreator(options);
//string outputScript = await scriptCreator.CreateScriptAsync(hardcodedKey).ConfigureAwait(false);
//Console.WriteLine($"script created with name {outputScript}");

var scriptCleaner = new ScriptCleaner(options);
var cleanedScriptKey = await scriptCleaner.CleanScriptAsync(hardcodedScriptName);
Console.WriteLine($"Cleaned script with name {cleanedScriptKey}");
