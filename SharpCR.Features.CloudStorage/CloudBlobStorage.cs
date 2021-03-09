using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using SharpCR.Features.CloudStorage.Transport;
using SharpCR.Registry;

namespace SharpCR.Features.CloudStorage
{
    public class CloudBlobStorage: IBlobStorage
    {
        private readonly CloudStorageConfiguration _config;
        private readonly HttpClient _httpClient;

        public CloudBlobStorage(IOptions<CloudStorageConfiguration> configuredOptions)
        {
            _config = configuredOptions.Value;
            if (string.IsNullOrEmpty(_config.SecretId) || string.IsNullOrEmpty(_config.SecretKey) || string.IsNullOrEmpty(_config.CosServiceBaseUrl))
            {
                throw new ArgumentException("Please specify Tencent Cloud COS credential correctly.");
            }
            _config.CosServiceBaseUrl = _config.CosServiceBaseUrl.TrimEnd('/');
            _httpClient = new HttpClient();
        }

        public async Task<Stream> ReadAsync(string location)
        {
            var downloadableUrl = await GenerateDownloadUrlAsync(location);
            var response = await _httpClient.GetAsync(downloadableUrl);
            return await response.Content.ReadAsStreamAsync();
        }

        public Task DeleteAsync(string location)
        {
            throw new NotImplementedException();
        }

        public async Task<string> SaveAsync(string repoName, string digest, Stream stream)
        {
            var objectKey =  Path.Combine(repoName, digest.Replace(':', '/'));
            var uri = $"{_config.CosServiceBaseUrl}/{objectKey}";
            
            var md5Hash = MD5Hash(stream);
            stream.Seek(0, SeekOrigin.Begin);
            
            var request = new HttpRequestMessage(HttpMethod.Put, uri);
            request.Content = new StreamContent(stream);
            request.Content.Headers.ContentLength = stream.Length; 
            request.Content.Headers.ContentMD5 = md5Hash; 
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");

            var signature = QcloudCosSigner.GenerateSignature(request, _config.SecretId, _config.SecretKey, false);
            request.Headers.TryAddWithoutValidation("Authorization", signature);
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return objectKey;
        }

        private static byte[] MD5Hash(Stream stream)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            return md5.ComputeHash(stream);
        }

        public bool SupportsDownloading { get; } = true;
        
        public Task<string> GenerateDownloadUrlAsync(string location)
        {
            var resourceUri = $"{_config.CosServiceBaseUrl}/{location}";
            var request = new HttpRequestMessage(HttpMethod.Get, resourceUri);
            var signature = QcloudCosSigner.GenerateSignature(request, _config.SecretId, _config.SecretKey, true);
            var downloadableUri = $"{resourceUri}?{signature}";
            return Task.FromResult(downloadableUri);
        }
    }
}
