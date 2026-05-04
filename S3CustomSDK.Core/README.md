# S3CustomSDK

S3CustomSDK is a lightweight, high-performance .NET Core SDK designed to interact with S3-compatible object storage platforms. It provides a simple and intuitive interface for managing buckets and objects across various providers, including Amazon S3, Minio, and RustFS.

## Features

- **Multi-Platform Support**: Seamlessly connect to Amazon S3, Minio, RustFS, and other S3-compatible storages.
- **Bucket Management**: Create and delete buckets with ease.
- **Object Operations**: Upload (Put), Download (Get), and Delete objects.
- **Folder Support**: Create folders and organize objects within directories.
- **Automatic Signing**: Integrated AWS Signature Version 4 signing for secure requests.
- **Stream Support**: Efficiently handle large files using streams.
- **Listing**: List objects within a bucket with XML parsing support.

## Installation

You can install the package via NuGet:

```bash
dotnet add package s3.customsdk
```

## Configuration

To get started, you need to configure the `S3Config` object with your credentials and endpoint.

```csharp
using S3CustomSDK.Core;

var config = new S3Config
{
    AccessKey = "YOUR_ACCESS_KEY",
    SecretKey = "YOUR_SECRET_KEY",
    Region = "us-east-1",
    Endpoint = "https://your-s3-endpoint.com",
    Service = "s3"
};

var client = new S3Client(config);
```

## Usage Examples

### Bucket Operations

#### Create a Bucket
```csharp
await client.CreateBucketAsync("my-new-bucket");
```

#### Delete a Bucket
```csharp
await client.DeleteBucketAsync("my-new-bucket");
```

### Object Operations

#### Upload an Object
```csharp
using var fileStream = File.OpenRead("path/to/file.txt");
await client.PutObjectAsync("my-bucket", "documents/file.txt", fileStream);
```

#### Download an Object
```csharp
using var stream = await client.GetObjectAsync("my-bucket", "documents/file.txt");
using var fileStream = File.Create("downloaded_file.txt");
await stream.CopyToAsync(fileStream);
```

#### Delete an Object
```csharp
await client.DeleteObjectAsync("my-bucket", "documents/file.txt");
```

### Folder and Organization

#### Create a Folder
```csharp
await client.CreateFolderAsync("my-bucket", "images");
```

#### Upload into a Folder
```csharp
using var stream = File.OpenRead("photo.jpg");
await client.PutObjectInFolderAsync("my-bucket", "images", "photo.jpg", stream, createFolder: true);
```

### Listing Objects
```csharp
var objects = await client.ListObjectsAsync("my-bucket");
foreach (var key in objects)
{
    Console.WriteLine($"Found object: {key}");
}
```

## License

This project is licensed under the [MIT License](LICENSE).

## Authors

- **Murat Girgin** - [GitHub Profile](https://github.com/muratg75)

---
*Developed for .NET Core applications requiring a custom, lightweight S3 client.*
