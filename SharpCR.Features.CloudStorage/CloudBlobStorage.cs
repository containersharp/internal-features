using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
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
            if (_config.CdnConfig != null && _config.CdnConfig.IsValid())
            {
                _config.CdnConfig.BaseUrl = _config.CdnConfig.BaseUrl.TrimEnd('/');
            }

            _httpClient = new HttpClient();
        }

        public async Task<Stream> ReadAsync(string location)
        {
            var downloadableUrl = GenerateCosDownloadUrl(location);
            var response = await _httpClient.GetAsync(downloadableUrl);
            return await response.Content.ReadAsStreamAsync();
        }

        public Task<bool> ExistAsync(string location)
        {
            throw new NotImplementedException();
        }

        public Task DeleteAsync(string location)
        {
            throw new NotImplementedException();
        }

        public async Task<string> SaveAsync(string repoName, string digest, Stream stream)
        {
            var objectKey =  digest.Replace(':', '/');
            var cosBaseUrl = _config.CosServiceBaseUrl;
            if (!string.IsNullOrEmpty(_config.AcceleratedUploadingBaseUrl))
            {
                cosBaseUrl = _config.AcceleratedUploadingBaseUrl;
            }
            var uri = $"{cosBaseUrl}/{objectKey}";
            
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
            var useCdn = _config.CdnConfig != null && _config.CdnConfig.IsValid();
            var downloadableUri = useCdn
                ? GenerateCdnDownloadUrl(location)
                : GenerateCosDownloadUrl(location);

            return Task.FromResult(downloadableUri);
        }
        
        string GenerateCosDownloadUrl(string location)
        {
            var resourceUri = $"{_config.CosServiceBaseUrl}/{location}";
            var request = new HttpRequestMessage(HttpMethod.Get, resourceUri);
            var signature = QcloudCosSigner.GenerateSignature(request, _config.SecretId, _config.SecretKey, true);
            return $"{resourceUri}?{signature}";
        }
        
        /// <summary>
        /// 按照腾讯云的约定，生成带鉴权的 CDN 下载 URL
        /// 文档位置：https://cloud.tencent.com/document/product/228/41622
        /// </summary>
        /// <param name="location"></param>
        /// <returns></returns>
        string GenerateCdnDownloadUrl(string location)
        {
            var timestamp = QcloudCosSigner.Timestamp(TimeSpan.Zero);
            var randString = Guid.NewGuid().ToString("N");
            
            var stringToSign = $"/{location}-{timestamp}-{randString}-0-{_config.CdnConfig.AuthKeyTypeA}";
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(stringToSign));
            var md5 = MD5Hash(ms).ToHexString();
            
            var resourceUri = $"{_config.CdnConfig.BaseUrl}/{location}?sign={timestamp}-{randString}-0-{md5}";
            return resourceUri;
        }
    }
}
