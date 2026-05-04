using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace S3CustomSDK.Core
{
    public class S3Client
    {
        private readonly S3Config _config;
        private readonly HttpClient _httpClient;
        private readonly AwsSigner _signer;

        public S3Client(S3Config config)
        {
            _config = config;
            _httpClient = new HttpClient();
            _signer = new AwsSigner(config);
        }

        public async Task CreateBucketAsync(string bucketName)
        {
            var requestUri = new Uri($"{_config.Endpoint.TrimEnd('/')}/{bucketName}/");
            using var request = new HttpRequestMessage(HttpMethod.Put, requestUri);
            
            // Sign the request with empty payload hash
            _signer.Sign(request, "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Request failed with {response.StatusCode}: {errorContent}");
            }
        }

        public async Task DeleteBucketAsync(string bucketName)
        {
            var requestUri = new Uri($"{_config.Endpoint.TrimEnd('/')}/{bucketName}/");
            using var request = new HttpRequestMessage(HttpMethod.Delete, requestUri);

            _signer.Sign(request, "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"DeleteBucket failed with {response.StatusCode}: {errorContent}");
            }
        }

        public async Task PutObjectAsync(string bucketName, string key, Stream content)
        {
            var requestUri = new Uri($"{_config.Endpoint.TrimEnd('/')}/{bucketName}/{key}");
            
            Stream uploadStream;
            if (content.CanSeek)
            {
                uploadStream = content;
            }
            else
            {
                var ms = new MemoryStream();
                await content.CopyToAsync(ms);
                ms.Position = 0;
                uploadStream = ms;
            }

            string contentSha256;
            using (var sha256 = SHA256.Create())
            {
                // Calculate hash
                long originalPosition = uploadStream.Position;
                var hashBytes = sha256.ComputeHash(uploadStream);
                contentSha256 = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                uploadStream.Position = originalPosition;
            }

            using var request = new HttpRequestMessage(HttpMethod.Put, requestUri);
            request.Content = new StreamContent(uploadStream);
            
            _signer.Sign(request, contentSha256);

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"PutObject failed with {response.StatusCode}: {errorContent}");
            }
        }

        public async Task DeleteObjectAsync(string bucketName, string key)
        {
            var requestUri = new Uri($"{_config.Endpoint.TrimEnd('/')}/{bucketName}/{key}");
            using var request = new HttpRequestMessage(HttpMethod.Delete, requestUri);

            _signer.Sign(request, "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"DeleteObject failed with {response.StatusCode}: {errorContent}");
            }
        }

        public async Task<Stream> GetObjectAsync(string bucketName, string key)
        {
            var requestUri = new Uri($"{_config.Endpoint.TrimEnd('/')}/{bucketName}/{key}");
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

            _signer.Sign(request, "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");

            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"GetObject failed with {response.StatusCode}: {errorContent}");
            }

            return await response.Content.ReadAsStreamAsync();
        }

        public async Task<List<string>> ListObjectsAsync(string bucketName)
        {
            var requestUri = new Uri($"{_config.Endpoint.TrimEnd('/')}/{bucketName}/?list-type=2");
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

            _signer.Sign(request, "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"ListObjects failed with {response.StatusCode}: {errorContent}");
            }
            
            var content = await response.Content.ReadAsStringAsync();
            var doc = XDocument.Parse(content);
            XNamespace ns = doc.Root.Name.Namespace;
            return doc.Descendants(ns + "Key").Select(x => x.Value).ToList();
        }


        public async Task CreateFolderAsync(string bucketName, string folderName)
        {
            if (!folderName.EndsWith("/")) folderName += "/";
            using var stream = new MemoryStream(new byte[0]);
            await PutObjectAsync(bucketName, folderName, stream);
        }

        public async Task PutObjectInFolderAsync(string bucketName, string folder, string fileName, Stream content, bool createFolder = false)
        {
             var key = string.IsNullOrEmpty(folder) ? fileName : $"{folder.TrimEnd('/')}/{fileName}";
             
             if (createFolder && !string.IsNullOrEmpty(folder))
             {
                 await CreateFolderAsync(bucketName, folder);
             }

             await PutObjectAsync(bucketName, key, content);
        }
    }
}
