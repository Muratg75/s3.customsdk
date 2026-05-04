using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using S3CustomSDK.Core;

namespace S3CustomSDK.Tests
{
    public class S3ClientTests
    {
        private readonly S3Client _client;
        private readonly S3Config _config;

        public S3ClientTests()
        {
            //_config = new S3Config
            //{
            //    Endpoint = "http://127.0.0.1:9000",
            //    AccessKey = "rustfsadmin", // Default RustFS credentials
            //    SecretKey = "rustfsadmin", // Default RustFS credentials
            //    Region = "us-east-1"
            //};

            _config = new S3Config
            {
                Endpoint = "http://127.0.0.1:9000",
                AccessKey = "minioadmin", // Default minio credentials
                SecretKey = "minioadmin", // Default minio credentials
                Region = "us-east-1"
            };
            _client = new S3Client(_config);
        }

        [Fact]
        public async Task TestFullLifecycle()
        {
            var bucketName = $"test-bucket-{Guid.NewGuid().ToString().ToLowerInvariant()}";
            var objectKey = "hello.txt";
            var objectContent = "Hello S3!";


            // 1. Create Bucket
            try
            {
                await _client.CreateBucketAsync(bucketName);
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                 // Start minio if not running? 
                 // We can't start it easily from here. Just fail with clear message.
                 throw new Exception($"Failed to connect to S3 endpoint at {_config.Endpoint}. Ensure MinIO is running.", ex);
            }

            // 2. Put Object
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(objectContent)))
            {
                await _client.PutObjectAsync(bucketName, objectKey, stream);
            }

            // 3. Delete Object
           await _client.DeleteObjectAsync(bucketName, objectKey);

            // 4. Delete Bucket
            await _client.DeleteBucketAsync(bucketName);
        }

        [Fact]
        public async Task TestListAndDownload()
        {
            var bucketName = $"test-bucket-dl-{Guid.NewGuid().ToString().ToLowerInvariant()}";
            var objectKey = "download-test.txt";
            var folder = "manual-folder";
            var fullKey = $"{folder}/{objectKey}";
            var contentString = "Content to list and download";

            await _client.CreateBucketAsync(bucketName);
            try
            {
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(contentString)))
                {
                    // Manually constructing key to test PutObjectAsync raw
                    await _client.PutObjectAsync(bucketName, fullKey, ms);
                }

                // Test List
                var keys = await _client.ListObjectsAsync(bucketName);
                Assert.Contains(fullKey, keys);

                // Test Get
                using (var stream = await _client.GetObjectAsync(bucketName, fullKey))
                using (var reader = new StreamReader(stream))
                {
                    var downloaded = await reader.ReadToEndAsync();
                    Assert.Equal(contentString, downloaded);
                }
            }
            finally
            {
                 // Clean up
                try 
                {
                     await _client.DeleteObjectAsync(bucketName, fullKey);
                     await _client.DeleteBucketAsync(bucketName);
                }
                catch {} // Ignore cleanup errors
            }
        }

        [Fact]
        public async Task TestFolderCreation()
        {
            var bucketName = $"test-bucket-folder-{Guid.NewGuid().ToString().ToLowerInvariant()}";
            var folderName = "my-folder";
            var fileName = "file-in-folder.txt";
            var contentString = "Content inside folder";

            await _client.CreateBucketAsync(bucketName);
            try
            {
                // Upload file and request folder creation
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(contentString)))
                {
                    await _client.PutObjectInFolderAsync(bucketName, folderName, fileName, ms, createFolder: true);
                }

                // Check if folder object exists (ends with /)
                // Note: ListObjects might show it.
                // Or we can try to get it, but GetObject on a folder usually returns 0 bytes.
                
                var keys = await _client.ListObjectsAsync(bucketName);
                Assert.Contains($"{folderName}/", keys); // The folder object
                Assert.Contains($"{folderName}/{fileName}", keys); // The file object
            }
            finally
            {
                try
                {
                    await _client.DeleteObjectAsync(bucketName, $"{folderName}/{fileName}");
                    await _client.DeleteObjectAsync(bucketName, $"{folderName}/");
                    await _client.DeleteBucketAsync(bucketName);
                }
                catch {}
            }
        }
    }
}
