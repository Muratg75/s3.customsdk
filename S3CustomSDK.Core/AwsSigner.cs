using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace S3CustomSDK.Core
{
    public class AwsSigner
    {
        private readonly S3Config _config;

        public AwsSigner(S3Config config)
        {
            _config = config;
        }

        public void Sign(HttpRequestMessage request, string contentSha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855") // Empty string hash default
        {
            var now = DateTime.UtcNow;
            var datestamp = now.ToString("yyyyMMdd");
            var amzDate = now.ToString("yyyyMMddTHHmmssZ");

            // Add standard headers
            request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
            request.Headers.TryAddWithoutValidation("x-amz-content-sha256", contentSha256);
            
            // Host header is usually set by HttpClient but needed for signing
            if (!request.Headers.Contains("Host") && request.RequestUri != null)
            {
                request.Headers.Host = request.RequestUri.Host + (request.RequestUri.IsDefaultPort ? "" : ":" + request.RequestUri.Port);
            }

            // 1. Canonical Request
            var canonicalUri = GetCanonicalUri(request.RequestUri);
            var canonicalQueryString = GetCanonicalQueryString(request.RequestUri);
            var (canonicalHeaders, signedHeaders) = GetCanonicalHeaders(request);
            var canonicalRequest = $"{request.Method}\n{canonicalUri}\n{canonicalQueryString}\n{canonicalHeaders}\n{signedHeaders}\n{contentSha256}";

            // 2. String to Sign
            var credentialScope = $"{datestamp}/{_config.Region}/s3/aws4_request";
            var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{credentialScope}\n{Sha256Hex(canonicalRequest)}";

            // 3. Calculate Signature
            var signingKey = GetSignatureKey(_config.SecretKey, datestamp, _config.Region, "s3");
            var signature = HmacSha256Hex(signingKey, stringToSign);

            // 4. Authorization Header
            var authHeader = $"AWS4-HMAC-SHA256 Credential={_config.AccessKey}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";
            request.Headers.TryAddWithoutValidation("Authorization", authHeader);
        }

        private string GetCanonicalUri(Uri uri)
        {
            if (string.IsNullOrEmpty(uri.AbsolutePath)) return "/";
            // S3 expects URI path to be normalized, but not fully decoded if it contains encoded slashes?
            // Usually we just take absolute path.
            // For simple paths like /bucket/key works fine.
            // We need to ensure we don't have double slashes unless intended?
            // Uri.AbsolutePath is already decoded. We need to re-encode it appropriately.
            // Simple approach: Split by '/', encode segments, join.
            var path = uri.AbsolutePath;
            var segments = path.Split('/');
            var encodedSegments = segments.Select(s => Uri.EscapeDataString(s));
            var encodedPath = string.Join("/", encodedSegments);
            // .NET Split leads to empty first segment if path starts with /. Join will restore it somewhat but we might get //
            if (path.StartsWith("/")) return "/" + string.Join("/", segments.Skip(1).Select(Uri.EscapeDataString));
            return encodedPath;
        }

        private string GetCanonicalQueryString(Uri uri)
        {
            if (string.IsNullOrEmpty(uri.Query)) return "";
            var query = uri.Query.TrimStart('?');
            var parameters = query.Split('&').Where(x => !string.IsNullOrEmpty(x)).Select(p => 
            {
                var parts = p.Split('=');
                var key = Uri.EscapeDataString(System.Net.WebUtility.UrlDecode(parts[0]));
                var value = parts.Length > 1 ? Uri.EscapeDataString(System.Net.WebUtility.UrlDecode(parts[1])) : "";
                return new KeyValuePair<string, string>(key, value);
            }).OrderBy(kvp => kvp.Key, StringComparer.Ordinal).ThenBy(kvp => kvp.Value, StringComparer.Ordinal);

            return string.Join("&", parameters.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        }

        private (string headers, string signedHeaders) GetCanonicalHeaders(HttpRequestMessage request)
        {
            var headers = new List<KeyValuePair<string, string>>();

            // Combine standard headers and content headers
            foreach (var header in request.Headers)
            {
                headers.Add(new KeyValuePair<string, string>(header.Key.ToLowerInvariant(), string.Join(",", header.Value).Trim()));
            }
            if (request.Content != null)
            {
                foreach (var header in request.Content.Headers)
                {
                    headers.Add(new KeyValuePair<string, string>(header.Key.ToLowerInvariant(), string.Join(",", header.Value).Trim()));
                }
            }

            var sortedHeaders = headers.OrderBy(h => h.Key, StringComparer.Ordinal).ToList();
            var headerString = new StringBuilder();
            var signedHeaderList = new List<string>();

            foreach (var header in sortedHeaders)
            {
                headerString.Append($"{header.Key}:{header.Value}\n");
                signedHeaderList.Add(header.Key);
            }

            return (headerString.ToString(), string.Join(";", signedHeaderList));
        }

        private static byte[] GetSignatureKey(string key, string dateStamp, string regionName, string serviceName)
        {
            var kSecret = Encoding.UTF8.GetBytes("AWS4" + key);
            var kDate = HmacSha256(kSecret, dateStamp);
            var kRegion = HmacSha256(kDate, regionName);
            var kService = HmacSha256(kRegion, serviceName);
            return HmacSha256(kService, "aws4_request");
        }

        private static byte[] HmacSha256(byte[] key, string data)
        {
            using (var hmac = new HMACSHA256(key))
            {
                return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            }
        }
        
        private static string HmacSha256Hex(byte[] key, string data)
        {
             return ToHex(HmacSha256(key, data));
        }

        private static string Sha256Hex(string data)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
                return ToHex(bytes);
            }
        }

        private static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder();
            foreach (var b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
