using System.IO;
using SharpCR.Features.CloudStorage.Transport;
using Xunit;

namespace SharpCR.Features.Tests.CloudStorage
{
    public class FixedLengthStreamFacts
    {

        [Fact]
        public void should_copy_fixed_length()
        {   
            var filePath =@"C:\Users\jijie\Projects\sharpcr-project\sharpcr\SharpCR.Registry\app_data\uploading-blobs\local_f6bbb9d775d74557a04d9c44990b0224";
            var outputPath = filePath + ".out";
            var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var totalLength = fs.Length;

            var fls = new FixedLengthStream(fs, 10485760);
            var fsOut = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            fls.CopyTo(fsOut);
            
            fsOut.Dispose();
            fls.Dispose();

            Assert.Equal(10485760, new FileInfo(outputPath).Length);
        }
        
        [Fact]
        public void should_copy_limited_to_original_file()
        {   
            var filePath =@"C:\Users\jijie\Projects\sharpcr-project\sharpcr\SharpCR.Registry\app_data\uploading-blobs\local_f6bbb9d775d74557a04d9c44990b0224";
            var outputPath = filePath + ".out";
            var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var totalLength = fs.Length;
            fs.Seek(10485760 * 7, SeekOrigin.Begin);
            
            var fls = new FixedLengthStream(fs, 10485760);
            var fsOut = File.Open(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            fls.CopyTo(fsOut);
            
            fsOut.Dispose();
            fls.Dispose();

            Assert.Equal(totalLength - (10485760 * 7), new FileInfo(outputPath).Length);
        }
    }
}