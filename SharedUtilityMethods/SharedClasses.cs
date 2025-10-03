using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedUtilityMethods
{
    /// <summary>
    /// This is the output of the first lambda function which splits the video into frames. That lambda function is called VideoScreenshotter.
    /// The output of that function will be used as the input to this function
    /// </summary>
    /// <param name="BucketName"></param>
    /// <param name="Prefix"></param>
    /// <param name="NumberOfFramesExtracted"></param>
    public record ProcessVideoOutput(string BucketName, string Prefix, double NumberOfFramesExtracted);

    /// <summary>
    /// The output of the ImageProcessor function
    /// </summary>
    /// <param name="BucketName">The bucket that contains the description of the screenshots.</param>
    /// <param name="S3Key">The name of the key which contains the content of the description of the screenshots.</param>
    public record ImageProcessorOutput(string BucketName, string S3Key);

    /// <summary>
    /// The output of the ScriptCreator lambda function.
    /// </summary>
    /// <param name="BucketName">The bucket that the script has been written to.</param>
    /// <param name="S3Key">The name of the script.</param>
    public record ScriptCreatorOutput(string BucketName, string S3Key);

    /// <summary>
    /// The output of the ScriptCleaner lambda function
    /// </summary>
    /// <param name="BucketName">The bucket which contains the cleaned script.</param>
    /// <param name="S3Key">The key of the cleaned script file.</param>
    public record ScriptCleanerOutput(string BucketName, string S3Key);
}
