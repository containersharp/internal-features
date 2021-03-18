namespace SharpCR.Features.SyncIntegration
{
    public class SyncConfiguration
    {
        public long? SyncTimeoutSeconds { get; set; } = 120;
        public string MirrorModeBaseDomain { get; set; }
        public DispatcherConfiguration Dispatcher { get; set; }


        public class DispatcherConfiguration
        {
            public string BaseUrl { get; set; }
            public string AuthorizationToken { get; set; }
        }
    }
    
    
}