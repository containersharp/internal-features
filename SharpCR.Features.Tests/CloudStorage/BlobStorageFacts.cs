using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using SharpCR.Features.CloudStorage.Transport;
using SharpCR.Registry;
using Xunit;

namespace SharpCR.Features.Tests.CloudStorage
{
    public class BlobStorageFacts
    {


        [Fact]
        public void hmacSHA1()
        {
            var hmacSha1 = Requester.hmacSHA1("BQYIM75p8x0iWVFSIgqEKwFprpRSVHlz", "1557989151;1557996351");
            Assert.Equal("eb2519b498b02ac213cb1f3d1a3d27a3b3c9bc5f", hmacSha1);
        }
        
        [Fact]
        public void UrlParamList()
        {
            var uri = new Uri(
                "https://examplebucket-1250000000.cos.ap-beijing.myqcloud.com/exampleobject(%E8%85%BE%E8%AE%AF%E4%BA%91)");
            var (httpParameters, urlParamList) = Requester.URLParameters(uri);
            
            Assert.Equal(string.Empty, urlParamList);
            Assert.Equal(httpParameters, urlParamList);
        }
        
        
        [Fact]
        public void HeaderList()
        {
            var uri = new Uri(
                "https://examplebucket-1250000000.cos.ap-beijing.myqcloud.com/exampleobject(%E8%85%BE%E8%AE%AF%E4%BA%91)");
            var request = new HttpRequestMessage(HttpMethod.Put, uri);
            request.Headers.Date = DateTimeOffset.Parse("Thu, 16 May 2019 06:45:51 GMT");
            request.Content = new StringContent("ObjectContent");
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");
            request.Content.Headers.ContentMD5 = Convert.FromBase64String("mQ/fVh815F3k6TAUm8m0eg==");
            request.Headers.TryAddWithoutValidation("x-cos-acl", "private");
            request.Headers.TryAddWithoutValidation("x-cos-grant-read", "uin=\"100000000011\"");
            
            
            var (httpHeaders, headerList) = Requester.HttpHeaders(request.Headers, request.Content.Headers);
            
            Assert.Equal("content-length=13&content-md5=mQ%2FfVh815F3k6TAUm8m0eg%3D%3D&content-type=text%2Fplain&date=Thu%2C%2016%20May%202019%2006%3A45%3A51%20GMT&host=examplebucket-1250000000.cos.ap-beijing.myqcloud.com&x-cos-acl=private&x-cos-grant-read=uin%3D%22100000000011%22", httpHeaders);
            Assert.Equal("content-length;content-md5;content-type;date;host;x-cos-acl;x-cos-grant-read", headerList);
        }
        
        
        
        
        
        
        
        
        
        
        
        
        [Fact]
        public async Task ShouldSaveBlob()
        {
            var storage = CreateBlobStorage(out var blobsPath);

            var bytes = Encoding.Default.GetBytes(Guid.NewGuid().ToString("N"));
            await using var ms = new MemoryStream(bytes);
            var location = await storage.SaveAsync("abc/foo", "sha256@ab123de", ms);

            var actualPath = Path.Combine(blobsPath, location);
            Assert.True(File.Exists(actualPath));
            Assert.True((await File.ReadAllBytesAsync(actualPath)).SequenceEqual(bytes));
        }
        
        [Fact]
        public async Task ShouldReadByLocation()
        {
            var storage = CreateBlobStorage(out _);

            var bytes = Encoding.Default.GetBytes(Guid.NewGuid().ToString("N"));
            await using var ms = new MemoryStream(bytes);
            var location = await storage.SaveAsync("abc/foo", "sha256@ab123de", ms);

            await using var readResult = await storage.ReadAsync(location);
            await using var readMs = new MemoryStream();
            await readResult.CopyToAsync(readMs);
            var readBytes = readMs.ToArray();
            
            Assert.True(readBytes.SequenceEqual(bytes));
        }


        [Fact]
        public void ShouldSupportDownloadsURL()
        {
            var storage = CreateBlobStorage(out _);
            
            Assert.True(storage.SupportsDownloading);
            Assert.Throws<NotImplementedException>(() => { storage.GenerateDownloadUrlAsync("some-location");});
        }
        
        
        private static IBlobStorage CreateBlobStorage(out string blobsPath)
        {
            throw new NotImplementedException();
        }
    }
}