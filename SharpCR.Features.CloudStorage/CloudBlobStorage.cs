using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharpCR.Features.CloudStorage.Transport;

namespace SharpCR.Features.CloudStorage
{
    public class CloudBlobStorage: IBlobStorage
    {
        private readonly CloudStorageConfiguration _config;
        private readonly HttpClient _httpClient;
        private const int MultipartUploadPartSize = 10485760;  // 10M
        private const int MultipartUploadBatchSize = 6; 
        
        private readonly ILogger<CloudBlobStorage> _logger;

        public CloudBlobStorage(IOptions<CloudStorageConfiguration> configuredOptions, ILogger<CloudBlobStorage> logger)
        {
            _logger = logger;
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

            _httpClient = new HttpClient {Timeout = TimeSpan.FromMinutes(60)};
        }

        public async Task<string> TryLocateExistingAsync(string digest)
        {
            var (objectKey, uri) = GetCloudObjectKey(digest);
            var request = new HttpRequestMessage(HttpMethod.Head, uri);
            var signature = QcloudCosSigner.GenerateSignature(request, _config.SecretId, _config.SecretKey, false);
            request.Headers.TryAddWithoutValidation("Authorization", signature);
            var response = await _httpClient.SendAsync(request);
            
            _logger.LogDebug("Trying to locating blob {@uri}. result: {@status}", uri, response.StatusCode);
            return response.StatusCode == HttpStatusCode.OK ? objectKey : null;
        }

        public async Task<Stream> ReadAsync(string location)
        {
            var downloadableUrl = GenerateCosDownloadUrl(location);
            var response = await _httpClient.GetAsync(downloadableUrl);
            
            _logger.LogDebug("Reading blob content {@uri}.", downloadableUrl);
            return await response.Content.ReadAsStreamAsync();
        }

        public async Task DeleteAsync(string location)
        {
            var resourceUri = $"{_config.CosServiceBaseUrl}/{location}";
            var request = new HttpRequestMessage(HttpMethod.Delete, resourceUri);
            var signature = QcloudCosSigner.GenerateSignature(request, _config.SecretId, _config.SecretKey, false);
            request.Headers.TryAddWithoutValidation("Authorization", signature);
            
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation("Blob deleted {@uri}.", resourceUri);
        }

        public async Task<string> SaveAsync(FileInfo temporaryFile, string repoName, string digest)
        {
            var (objectKey, uri) = GetCloudObjectKey(digest);
            
            if (temporaryFile.Length < MultipartUploadPartSize)
            {
                await MonolithicUploadAsync(temporaryFile, uri);
            }
            else
            {
                await BatchUploadAsync(temporaryFile, uri);
            }

            return objectKey;
        }

        private async Task MonolithicUploadAsync(FileInfo file, string uri)
        {
            const int maxRetries = 3;
            var timeTried = 0;

            byte[] md5Hash = null;
            Exception lastException = null;
            FileStream fs = null;
            while (++timeTried < maxRetries)
            {
                try
                {
                    fs = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
                    md5Hash ??= Md5Hash(fs);

                    fs.Seek(0, SeekOrigin.Begin);
                    var request = new HttpRequestMessage(HttpMethod.Put, uri) {Content = new StreamContent(fs)};
                    request.Content.Headers.ContentLength = file.Length;
                    request.Content.Headers.ContentMD5 = md5Hash;
                    request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");

                    var signature = QcloudCosSigner.GenerateSignature(request, _config.SecretId, _config.SecretKey, false);
                    request.Headers.TryAddWithoutValidation("Authorization", signature);
                    var response = await _httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    _logger.LogInformation("Successfully completed monolithic blob upload {@uri}.", uri);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Failed to complete monolithic blob upload {@upload}. error: {@error}",
                        new { uri, size = file.Length, temporaryFile = file.FullName, uploadId = (string)null }, ex);
                    lastException = ex;
                }
                finally
                {
                    fs?.Dispose();
                }
                await Task.Delay(TimeSpan.FromSeconds(3));
            }

            throw lastException!;
        }

        private async Task BatchUploadAsync(FileInfo temporaryFile, string baseUri)
        {
            var partCount = (temporaryFile.Length / MultipartUploadPartSize) + (temporaryFile.Length % MultipartUploadPartSize == 0 ? 0 : 1);
            if (partCount == 0)
            {
                return;
            }
            
            var uploadId = await InitializeMultipartUpload(baseUri);
            var indexes = new List<long>((int) partCount);
            for (var i = 0; i < partCount; i++)
            {
                indexes.Add(i * MultipartUploadPartSize);
            }

            try
            {
                var eTagList = new List<string>();
                var batches = indexes.Batch(MultipartUploadBatchSize).ToArray();
                foreach (var batch in batches)
                {
                    var tasks = batch.Select(streamOffset => UploadPart(temporaryFile, baseUri, uploadId, streamOffset)).ToArray();
                    await Task.WhenAll(tasks);
                    eTagList.AddRange(tasks.Select(t => t.Result));
                }
                await CompleteMultipartUpload(baseUri, uploadId, eTagList);
            }
            catch(Exception ex)
            {
                var uploadingTask = new {uri = baseUri, size = temporaryFile.Length, temporaryFile = temporaryFile.FullName, uploadId};
                _logger.LogWarning("Failed to upload {@blob}. error: {@error}",  uploadingTask,  ex);
                try
                {
                    await RemoveFailedPartUpload(baseUri, uploadId);
                }
                catch (Exception exNested)
                {
                    _logger.LogDebug("Failed to remove intermedium upload task {@blob}. error: {@error}", uploadingTask,  exNested);
                }
                throw;
            }
        }

        private async Task<string> InitializeMultipartUpload(string uri)
        {
            var uploadIdPattern = new Regex("<UploadId>(?<id>[^<]+)</UploadId>", RegexOptions.Compiled);
            var initUri = $"{uri}?uploads";
            var request = new HttpRequestMessage(HttpMethod.Post, initUri);
            request.Headers.TryAddWithoutValidation("Content-Length", "0");
            request.Headers.TryAddWithoutValidation("Content-Type", "application/octet-stream");

            var signature = QcloudCosSigner.GenerateSignature(request, _config.SecretId, _config.SecretKey, false);
            request.Headers.TryAddWithoutValidation("Authorization", signature);
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var uploadId = uploadIdPattern.Match(responseContent).Groups["id"].Value;
            _logger.LogDebug("Successfully initialized multipart upload session for blob {@upload}.", new { uri = initUri, uploadId });
            return uploadId;
        }

        private async Task<string> UploadPart(FileInfo temporaryFile, string baseUri, string uploadId, long streamOffset)
        {
            const int maxRetries = 3;
            var timeTried = 0;

            var partNumber = (streamOffset / MultipartUploadPartSize) + 1;
            var uploadUri = $"{baseUri}?partNumber={partNumber}&uploadId={uploadId}";
            byte[] md5Hash = null;

            FixedLengthStream fixedLengthStream = null;
            Exception lastException = null;
            while (++timeTried < maxRetries)
            {
                try
                {
                    var fs = temporaryFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
                    fs.Seek(streamOffset, SeekOrigin.Begin);
                    fixedLengthStream = new FixedLengthStream(fs, MultipartUploadPartSize);
                    md5Hash ??= Md5Hash(fixedLengthStream);

                    fixedLengthStream.Seek(0, SeekOrigin.Begin);
                    var request = new HttpRequestMessage(HttpMethod.Put, uploadUri) {Content = new StreamContent(fixedLengthStream)};
                    request.Content.Headers.ContentLength = fixedLengthStream.Length;
                    request.Content.Headers.ContentMD5 = md5Hash;
                    request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");

                    var signature =
                        QcloudCosSigner.GenerateSignature(request, _config.SecretId, _config.SecretKey, false);
                    request.Headers.TryAddWithoutValidation("Authorization", signature);
                    var response = await _httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    _logger.LogDebug("Successfully uploaded blob batch part {@uri}.", uploadUri);
                    return response.Headers.ETag?.Tag;
                }
                catch(Exception ex)
                {
                    _logger.LogDebug("Failed to upload blob batch part {@part}. error: {@error}", 
                        new {uri = uploadUri, size = temporaryFile.Length, temporaryFile = temporaryFile.FullName, uploadId}, ex);
                    lastException = ex;
                }
                finally
                {
                    fixedLengthStream?.Dispose();
                }
                await Task.Delay(TimeSpan.FromSeconds(3));
            }

            throw lastException!;
        }

        private async Task CompleteMultipartUpload(string baseUri, string uploadId, List<string> partETags)
        {
            var completeUri = $"{baseUri}?uploadId={uploadId}";
            var contentBuilder = new StringBuilder("<CompleteMultipartUpload>");
            for (var i = 0; i < partETags.Count; i++)
            {
                contentBuilder.Append($"<Part><PartNumber>{i+1}</PartNumber><ETag>{partETags[i]}</ETag></Part>");
            }
            contentBuilder.Append("</CompleteMultipartUpload>");

            await using var ms = new MemoryStream(Encoding.Default.GetBytes(contentBuilder.ToString()));
            var md5Hash = Md5Hash(ms);
            ms.Seek(0, SeekOrigin.Begin);
            
            var request = new HttpRequestMessage(HttpMethod.Post, completeUri)
            {
                Content = new StreamContent(ms)
            };
            request.Content.Headers.ContentLength = ms.Length;
            request.Content.Headers.ContentMD5 = md5Hash;
            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/xml");

            var signature = QcloudCosSigner.GenerateSignature(request, _config.SecretId, _config.SecretKey, false);
            request.Headers.TryAddWithoutValidation("Authorization", signature);
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation("Successfully completed blob batch upload session {@uri}.", completeUri);
        }

        private async Task RemoveFailedPartUpload(string baseUri, string uploadId)
        {
            var deleteUri = $"{baseUri}?uploadId={uploadId}";
            var request = new HttpRequestMessage(HttpMethod.Delete, deleteUri);

            var signature = QcloudCosSigner.GenerateSignature(request, _config.SecretId, _config.SecretKey, false);
            request.Headers.TryAddWithoutValidation("Authorization", signature);
            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            _logger.LogDebug("Blob batch upload session deleted: {@uri}.", deleteUri);
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
            var md5 = Md5Hash(ms).ToHexString();
            
            var resourceUri = $"{_config.CdnConfig.BaseUrl}/{location}?sign={timestamp}-{randString}-0-{md5}";
            return resourceUri;
        }

        private (string objectKey, string uri) GetCloudObjectKey(string digest)
        {
            var indexOfColon = digest.IndexOf(':');
            var subDir = digest.Substring(indexOfColon + 1, 2);
            var objectKey = digest.Insert(indexOfColon + 1,$"{subDir}/").Replace(':', '/');
            var cosBaseUrl = _config.CosServiceBaseUrl;
            if (!string.IsNullOrEmpty(_config.AcceleratedUploadingBaseUrl))
            {
                cosBaseUrl = _config.AcceleratedUploadingBaseUrl;
            }

            var uri = $"{cosBaseUrl}/{objectKey}";
            return (objectKey, uri);
        }

        private static byte[] Md5Hash(Stream stream)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            return md5.ComputeHash(stream);
        }
    }

    public static class EnumerableExtensions
    {
        public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> items,  int batchSize)
        {
            return items.Select((item, idx) => new { item, inx = idx })
                .GroupBy(x => x.inx / batchSize)
                .Select(g => g.Select(x => x.item));
        }
    }
}