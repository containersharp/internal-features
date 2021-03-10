namespace SharpCR.Features.CloudStorage
{
    public class CloudStorageConfiguration
    {
        public string SecretId { get; set; }
        public string SecretKey { get; set; }
        public string CosServiceBaseUrl { get; set; }
        
        public string AcceleratedUploadingBaseUrl { get; set; }
        
        public CloudStorageCdnConfiguration CdnConfig { get; set; }
    }

    public class CloudStorageCdnConfiguration
    {
        public string BaseUrl { get; set; }
        /// <summary>
        /// 配置 CDN 访问鉴权时，要开启 CDN 回源鉴权和 CDN 鉴权配置两个选项。
        /// https://cloud.tencent.com/document/product/228/41622
        /// </summary>
        public string AuthKeyTypeA { get; set; }

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(BaseUrl) && !string.IsNullOrEmpty(AuthKeyTypeA);
        }
    }
}