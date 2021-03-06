using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace SharpCR.Features.CloudStorage.Transport
{
    public static class QcloudCosSigner
    {
        /// <summary>
        /// Get signed request according to doc at
        /// https://cloud.tencent.com/document/product/436/7778
        /// </summary>
        /// <returns></returns>
        public static string GenerateSignature(HttpRequestMessage requestMessage, string secretId, string secretKey, bool noHeaders)
        {
            var keyStart = Timestamp(TimeSpan.Zero);
            var keyEnd = Timestamp(TimeSpan.FromMinutes(30));
            var keyTime = $"{keyStart};{keyEnd}";

            requestMessage.Headers.Host = requestMessage.RequestUri.Host;
            requestMessage.Headers.Date = DateTimeOffset.UtcNow;

            var signKey = hmacSHA1(secretKey, keyTime);
            var (httpParameters, urlParamList) = URLParameters(requestMessage.RequestUri);
            var (httpHeaders, headerList) = HttpHeaders(noHeaders ? null : requestMessage.Headers, requestMessage.Content?.Headers);

            var httpString = string.Join("\n",  new[]
            {
                requestMessage.Method.ToString().ToLower(),
                requestMessage.RequestUri.AbsolutePath,
                httpParameters,
                httpHeaders,
                string.Empty
            });
            var sha1HttpString = SHA1(httpString);

            var stringToSign = string.Join("\n", new[]
            {
                "sha1",
                keyTime,
                sha1HttpString,
                string.Empty
            });

            var signature = hmacSHA1( signKey, stringToSign);
            var result = string.Join("&", new[]
            {
                "q-sign-algorithm=sha1",
                $"q-ak={secretId}",
                $"q-sign-time={keyTime}",
                $"q-key-time={keyTime}",
                $"q-header-list={headerList}",
                $"q-url-param-list={urlParamList}",
                $"q-signature={signature}"
            });
            return result;
        }

        public static long Timestamp(TimeSpan offset)
        {
            return DateTimeOffset.UtcNow.Add(offset).ToUnixTimeSeconds();
        }

        private static (string httpHeaders, string headerList) HttpHeaders(HttpHeaders requestHeaders, HttpHeaders contentHeaders)
        {
            var emptyHeaders = (Enumerable.Empty<Tuple<string, IEnumerable<string>>>());
            var requestHeaderVals = requestHeaders?
                .Select(h => Tuple.Create(h.Key, h.Value)) ?? emptyHeaders;
            var contentHeaderVals = contentHeaders?
                .Select(h => Tuple.Create(h.Key, h.Value))  ?? emptyHeaders ;
            
            var headerParameters = requestHeaderVals.Concat(contentHeaderVals)
                .OrderBy(h => h.Item1)
                .ToDictionary(h => UrlEncode(h.Item1)?.ToLower(),
                    h => UrlEncode(h.Item2.Single()),
                    StringComparer.Ordinal);
            var httpHeaders = headerParameters.Aggregate(string.Empty, (prev, kv) => $"{prev}&{kv.Key}={kv.Value}")
                .TrimStart('&');
            var headerList = string.Join(";", headerParameters.Keys);
            return (httpHeaders, headerList);
        }

        private  static (string httpParameters, string urlParamList) URLParameters(Uri requestUri)
        {
            var queryStringMap = ParseQueryString(requestUri.Query);
            if (queryStringMap.Count == 0)
            {
                return (string.Empty, string.Empty);
            }
            
            var urlParameters = queryStringMap.Keys
                .OrderBy(k => k)
                .ToDictionary(k => k.ToLower(),
                    k => string.IsNullOrEmpty(queryStringMap[k]) ? string.Empty : queryStringMap[k],
                    StringComparer.Ordinal);
            var httpParameters = urlParameters
                .Aggregate(string.Empty, (prev, kv) => $"{prev}&{kv.Key}={kv.Value}")
                .TrimStart('&');
            var urlParamList = string.Join(";", urlParameters.Keys);
            return (httpParameters, urlParamList);
        }

        private static Dictionary<string, string> ParseQueryString(string requestQuery)
        {
            var emptyDic = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(requestQuery))
            {
                return emptyDic;
            }
            
            if (requestQuery[0] == '?')
            {
                if (requestQuery.Length == 1)
                {
                    return emptyDic;
                }
                requestQuery = requestQuery.Substring(1);
            }

            return requestQuery.Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Split('=', StringSplitOptions.RemoveEmptyEntries))
                .ToDictionary(p => p[0], p => p.Length == 2 ? p[1] : null);
        }


        private  static string SHA1(string str)
        {
            using var sha1 = System.Security.Cryptography.SHA1.Create();
            return sha1.ComputeHash(Encoding.UTF8.GetBytes(str)).ToHexString();
        }

        private  static string hmacSHA1(string key, string str)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            using var hmacsha1 = new HMACSHA1(keyBytes);
            hmacsha1.Key = keyBytes;
            return hmacsha1.ComputeHash(Encoding.UTF8.GetBytes(str)).ToHexString();   
        }

        private static string UrlEncode(string raw)
        {
            var specialChars = new[] { '!', '(', ')', '*' };

            var encoded = WebUtility.UrlEncode(raw).Replace("+", "%20");
            foreach (var c in specialChars)
            {
                var integer = (int)c;
                encoded = encoded.Replace(new string(new[] {c}), $"%{integer:X2}");
            }

            return encoded;
        }


        public static string ToHexString(this byte[] bytes)
        {
            return bytes.Aggregate("", (s, e) => s + $"{e:x2}", s => s);
        }
    }
}