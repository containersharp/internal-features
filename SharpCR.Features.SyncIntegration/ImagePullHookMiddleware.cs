using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SharpCR.Features.Records;
using SharpCR.Manifests;

namespace SharpCR.Features.SyncIntegration
{
    public class ImagePullHookMiddleware: IMiddleware
    {
        private readonly IRecordStore _recordStore;
        private readonly SyncConfiguration _options;

        private static readonly Regex ManifestRegex = new Regex("^/v2/(?<repo>.+)/manifests/(?<ref>.+)$", 
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static HttpClient _httpClient;

        public ImagePullHookMiddleware(IOptions<SyncConfiguration> options, IRecordStore recordStore)
        {
            _options = options.Value;
            if (_options.Dispatcher?.BaseUrl == null)
            {
                throw new InvalidOperationException("Please specify Dispatcher configuration for the 'SyncIntegration' feature.");
            }
            _options.Dispatcher.BaseUrl = _options.Dispatcher.BaseUrl.TrimEnd('/');
            _recordStore = recordStore;
        }
        
        
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var requestPath = context.Request.Path.Value;
            var requestMethod = context.Request.Method.ToUpper();
            var isPullRequest = (requestMethod == "GET" || requestMethod == "HEAD")
                                && ManifestRegex.IsMatch(requestPath);

            if (!isPullRequest)
            {
                await next(context);
                return;
            }

            var requestInfo = ManifestRegex.Match(requestPath);
            var repoName = requestInfo.Groups["repo"].Value;
            var reference = requestInfo.Groups["ref"].Value;
            var (registry, actualRepoName) = ParseRegistryAndRepoName(context.Request, repoName);
            if (registry == null)
            {
                await next(context);
                return;
            }

            var imageExists = await CheckIfImageExistsAsync(registry, actualRepoName, reference);
            if (imageExists)
            {
                await next(context);
                return;
            }
            
            await WaitForSync(registry, actualRepoName, reference);
            await next(context);
        }


        private async Task<bool> CheckIfImageExistsAsync(string registry, string repoName, string imageReference)
        {
            var storageRepoName = $"{registry}/{repoName}";
            var existingItem = await GetArtifactByReferenceAsync(storageRepoName, imageReference);
            return existingItem != null;
        }

        private async Task WaitForSync(string registry, string repoName, string reference)
        {
            const string jsonMediaType = "application/json";
            if (_httpClient == null)
            {
                _httpClient = new HttpClient();
                _httpClient.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue.Parse(jsonMediaType));
            }

            var imageRepository = $"{registry}/{repoName}";
            var isDigest = IsDigestRef(reference);
            var probeRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.Dispatcher.BaseUrl}/probe");
            var probe = new SyncJob
            {
                ImageRepository = imageRepository,
                Tag = isDigest ? null : reference,
                Digest = isDigest ? reference : null
            };
            var jsonSettings = new JsonSerializerSettings
            {
                ContractResolver =  new DefaultContractResolver
                {
                    NamingStrategy = new CamelCaseNamingStrategy()
                },
                Formatting = Formatting.None
            };
            var jsonContent = JsonConvert.SerializeObject(probe, jsonSettings);
            probeRequest.Content = new StringContent(jsonContent, Encoding.UTF8, jsonMediaType);
            var probeResponse = await _httpClient.SendAsync(probeRequest);
            probeResponse.EnsureSuccessStatusCode();

            var probeResultContent = await probeResponse.Content.ReadAsStringAsync();
            var probeResult = JsonConvert.DeserializeObject<ProbeResult>(probeResultContent);
            var missingItems = new List<Manifest>();
            foreach (var item in probeResult.ManifestItems)
            {
                var itemExists = (null != await _recordStore.GetArtifactByDigestAsync(imageRepository, item.Digest));
                if (!itemExists)
                {
                    missingItems.Add(item);
                }
            }

            if (!missingItems.Any())
            {
                if (probeResult.ListManifest != null)
                {
                    await _recordStore.CreateArtifactAsync(new ArtifactRecord
                    {
                        Tag = isDigest ? null : reference,
                        DigestString = probeResult.ListManifest.Digest,
                        ManifestBytes = probeResult.ListManifest.RawJsonBytes,
                        ManifestMediaType = probeResult.ListManifest.MediaType,
                        RepositoryName = imageRepository
                    });
                }
                else
                {
                    // 探测结果不是 List，则必然是单个镜像
                    // 到达此处意味着，所有镜像都是存在的。也就是说，镜像实际上存在，但 tag 不存在（比如，v3.11 => latest）
                    if(isDigest)
                    {
                        return;
                    }
                    var item = probeResult.ManifestItems.Single();
                    var existingRecord = await _recordStore.GetArtifactByDigestAsync(imageRepository, item.Digest);
                    if (string.IsNullOrEmpty(existingRecord.Tag))
                    {
                        existingRecord.Tag = reference;
                        await _recordStore.UpdateArtifactAsync(existingRecord);
                    }
                    else
                    {
                        // 为相同的 Digest 创建一个 Tag 记录
                        await _recordStore.CreateArtifactAsync(new ArtifactRecord
                        {
                            Tag = reference,
                            DigestString = item.Digest,
                            ManifestBytes = item.RawJsonBytes,
                            ManifestMediaType = item.MediaType,
                            RepositoryName = imageRepository
                        });
                    }
                }
                return;
            }

            var jobs = missingItems.Select(item => new SyncJob
            {
                ImageRepository = imageRepository,
                Digest = item.Digest
            }).ToList();
            var jobsRequestContent = JsonConvert.SerializeObject(jobs, jsonSettings);
            var jobScheduleRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.Dispatcher.BaseUrl}/jobs")
            {
                Content = new StringContent(jobsRequestContent, Encoding.UTF8, jsonMediaType)
            };
            var jobResponse = await _httpClient.SendAsync(jobScheduleRequest);
            jobResponse.EnsureSuccessStatusCode();
            
            await SpinWaitForSync(imageRepository, missingItems.Select(i => i.Digest).ToArray());
        }


        private async Task SpinWaitForSync(string repoName, string[] digestList)
        {
            var maxWait = Task.Delay(TimeSpan.FromSeconds(_options.SyncTimeoutSeconds!.Value));
            while (!maxWait.IsCompleted)
            {
                var syncCompleted = await CheckSyncCompleted(repoName, digestList);
                if (syncCompleted)
                {
                    return;
                }
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
            // timeout
        }

        private async Task<bool> CheckSyncCompleted(string repoName, string[] digestList)
        {
            foreach (var digest in digestList)
            {
                var existingItem = await _recordStore.GetArtifactByDigestAsync(repoName, digest);
                if (null == existingItem)
                {
                    return false;
                }
            }

            return true;
        }

        private (string, string) ParseRegistryAndRepoName(HttpRequest request, string repoNameInUrl)
        {
            var mirrorMode = !string.IsNullOrEmpty(_options.MirrorModeBaseDomain);
            var parts = repoNameInUrl.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (parts.Count == 1)
            {
                parts.Insert(0, "library");
            }

            string registry, repoName;
            if (mirrorMode)
            {
                if (!request.Host.Host.EndsWith(_options.MirrorModeBaseDomain))
                {
                    return (null, null);
                }

                // foo.bar.base.domain
                registry = request.Host.Host.Substring(0,request.Host.Host.Length - _options.MirrorModeBaseDomain.Length - 1);
                repoName = string.Join('/', parts);
                return (registry, repoName);
            }

            if (parts.Count == 2)
            {
                parts.Insert(0, "docker.io");
            }

            registry = parts[0];
            repoName = string.Join('/', parts.Skip(1));
            return (registry, repoName);
        }
        
        private async Task<ArtifactRecord> GetArtifactByReferenceAsync(string repoName, string reference)
        {
            return IsDigestRef(reference)
                ? await _recordStore.GetArtifactByDigestAsync(repoName, reference)
                : await _recordStore.GetArtifactByTagAsync(repoName, reference);
        }

        private static bool IsDigestRef(string reference)
        {
            return Digest.TryParse(reference, out _);
        }


        // ReSharper disable UnusedAutoPropertyAccessor.Local
        class SyncJob
        {
            public string ImageRepository { get; set; }

            public string Tag { get; set; }

            public string Digest { get; set; }

            // todo: implement the auth token integration
            public string AuthorizationToken { get; set; }
        }
        public class ProbeResult
        {
            public ManifestV2List ListManifest { get; set; }
            public Manifest[] ManifestItems { get; set; }
        }
    }
}