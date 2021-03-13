using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SharpCR.Features.CloudStorage;
using Xunit;

namespace SharpCR.Features.Tests.CloudStorage
{
    public class BlobStorageFacts
    {
        private readonly CloudStorageConfiguration _config;

        public BlobStorageFacts()
        {
            // _config = {}
        }
        
        [Fact]
        public async Task ShouldSaveBlob()
        {
            var storage = new CloudBlobStorage(Options.Create(_config));

            var guid = Guid.NewGuid().ToString("N");
            var bytes = Encoding.Default.GetBytes(guid);
            await using var ms = new MemoryStream(bytes);
            var location = await storage.SaveAsync($"sha256:{guid}", ms, "abc/foo");

            Assert.NotEmpty(location);
        }
        
        [Fact]
        public async Task ShouldReadByLocation()
        {
            var storage = new CloudBlobStorage(Options.Create(_config));
            var location = "abc/foo/sha256/b5b2b2c507a0944348e0303114d8d93aaaa081732b86451d9bce1f432a537bc7";

            await using var readResult = await storage.ReadAsync(location);
            await using var readMs = new MemoryStream();
            await readResult.CopyToAsync(readMs);
            var readBytes = readMs.ToArray();
            
            Assert.Equal(32, readBytes.Length);
        }


        [Fact]
        public void ShouldSupportDownloadsURL()
        {
            var storage = new CloudBlobStorage(Options.Create(_config));
            Assert.True(storage.SupportsDownloading);
        }
        
        
        [Fact]
        public async Task ShouldGenearateDownloadsURL()
        {
            var storage = new CloudBlobStorage(Options.Create(_config));
            var location = "abc/foo/sha256/2c4cd5fce8d443c5a58e7f2505198f35";
            var downloableURL = await storage.GenerateDownloadUrlAsync(location);
            
            Assert.NotNull(downloableURL);
        }
        
    }
}