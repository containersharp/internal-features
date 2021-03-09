namespace SharpCR.Features.CloudStorage.Transport
{
    public class ErrorMessage
    {
        public string Code { get; set; }
        public string Message { get; set; }
        public string Resource { get; set; }
        public string RequestId { get; set; }
        public string TraceId { get; set; }
    }
}