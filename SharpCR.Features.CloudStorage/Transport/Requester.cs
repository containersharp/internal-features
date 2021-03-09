using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace SharpCR.Features.CloudStorage.Transport
{
    public class Requester
    {
        


        /// <summary>
        /// Get signed request according to doc at
        /// https://cloud.tencent.com/document/product/436/7778
        /// </summary>
        /// <returns></returns>
        public static string GenerateSignature(string secretId, string secretKey, HttpMethod httpMethod, Uri requestUri,
            HttpRequestHeaders requestHeaders,
            HttpContentHeaders contentHeaders)
        {
            // var keyStart = timestamp(TimeSpan.Zero);
            // var keyEnd = timestamp(TimeSpan.FromMinutes(60));
            // var keyTime = $"{keyStart};{keyEnd}";
            var keyTime = $"1557989151;1557996351";

            var signKey = hmacSHA1(secretKey, keyTime);

            var (httpParameters, urlParamList) = URLParameters(requestUri);


            var (httpHeaders, headerList) = HttpHeaders(requestHeaders, contentHeaders);


            var httpString = string.Join("\n",  new[]
            {
                httpMethod.ToString().ToLower(),
                requestUri.AbsolutePath,
                httpParameters,
                httpHeaders
            });
            var sha1HttpString = SHA1(httpString);

            var stringToSign = string.Join("\n", new[]
            {
                keyTime,
                sha1HttpString
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

        public static (string httpHeaders, string headerList) HttpHeaders(HttpHeaders requestHeaders, HttpHeaders contentHeaders)
        {
            var requestHeaderVals = requestHeaders
                .Select(h => Tuple.Create(h.Key, h.Value));
            var contentHeaderVals = contentHeaders
                .Select(h => Tuple.Create(h.Key, h.Value));
            
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

        public static (string httpParameters, string urlParamList) URLParameters(Uri requestUri)
        {
            var queryStringMap = HttpUtility.ParseQueryString(requestUri.Query); // queryString should all be UrlEncoded
            var urlParameters = queryStringMap.AllKeys
                .OrderBy(k => k)
                .ToDictionary(k => k.ToLower(),
                    k => string.IsNullOrEmpty(queryStringMap[k]) ? string.Empty : queryStringMap[k],
                    StringComparer.Ordinal);
            var httpParameters = urlParameters.Aggregate(string.Empty, (prev, kv) => $"{prev}&{kv.Key}={kv.Value}")
                .TrimStart('&');
            var urlParamList = string.Join(";", urlParameters.Keys);
            return (httpParameters, urlParamList);
        }


        public static long timestamp(TimeSpan offset)
        {
            return DateTimeOffset.UtcNow.Add(offset).ToUnixTimeSeconds();
        }

            
        public static string SHA1(string str)
        {
            using var sha1 = System.Security.Cryptography.SHA1.Create();
            return sha1.ComputeHash(Encoding.UTF8.GetBytes(str)).Aggregate("", (s, e) => s + $"{e:x2}", s => s );
        }

        public static string hmacSHA1(string key, string str)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            using var hmacsha1 = new HMACSHA1(keyBytes);
            hmacsha1.Key = keyBytes;
            return hmacsha1.ComputeHash(Encoding.UTF8.GetBytes(str)).Aggregate("", (s, e) => s + $"{e:x2}", s => s );   
        }

        private static string UrlEncode(string raw)
        {
            var specialChars = new[]
            {
                ' ',
                '!',
                '"',
                '#',
                '$',
                '%',
                '&',
                '\'',
                '(',
                ')',
                '*',
                '+',
                ',',
                '/',
                ':',
                ';',
                '<',
                '=',
                '>',
                '?',
                '@',
                '[',
                '\\',
                ']',
                '^',
                '`',
                '{',
                '|',
                '}'
            };

            var list = specialChars.Where(c =>
            {
                var str = new string(new[] {c});
                return HttpUtility.UrlEncode(str).Equals(str);
            }).ToList();

            var encoded = HttpUtility.UrlEncode(raw);
            list.ForEach(c =>
            {
                var integer = (int)c;
                encoded = encoded.Replace(new string(new []{c}), $"%{integer:x2}");
            });
            return encoded;
        }
    }
}