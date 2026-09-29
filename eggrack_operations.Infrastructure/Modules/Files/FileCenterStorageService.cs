using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Eggrack.Operations.Infrastructure.Modules.Files;

public sealed class FileCenterStorageService(IConfiguration configuration, IHostEnvironment environment, IHttpClientFactory httpClientFactory)
{
    private static readonly Regex RequestPathPattern = new("^[a-f0-9]{2}/[a-f0-9]{64}\\.bin$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<Stream> OpenReadAsync(FileCenterStoredFile file, CancellationToken token = default)
    {
        var publicFile = await TryOpenPublicAsync(file, token);
        if (publicFile is not null) return publicFile;

        return file.SourceKind switch
        {
            "request" => await OpenEncryptedRequestAsync(ResolveSiteRoot(), file, token),
            "plan" => OpenPlanFile(file),
            _ => throw new FileNotFoundException("不支持的文件来源。")
        };
    }

    public async Task<StoredPlanUpload> SavePlanFileAsync(uint planId,string originalName,string mimeType,Stream source,CancellationToken token=default)
    {
        var safeName=Path.GetFileName(originalName);
        if(string.IsNullOrWhiteSpace(safeName)||safeName.Length>255)throw new InvalidDataException("文件名无效或过长。");
        var fileRelative=$"{planId}/{Guid.NewGuid():N}.bin";
        var relative=$"managed/{fileRelative}";
        var path=ResolveInside(ResolvePlanUploadRoot(),fileRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        const long maximumBytes=50L*1024*1024;
        long length=0;
        try
        {
            await using var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,64*1024,FileOptions.Asynchronous);
            var buffer=new byte[64*1024];
            int read;
            while((read=await source.ReadAsync(buffer,token))>0)
            {
                length+=read;
                if(length>maximumBytes)throw new InvalidDataException("单个附件不能超过 50 MB。");
                await output.WriteAsync(buffer.AsMemory(0,read),token);
            }
        }
        catch { if(File.Exists(path))File.Delete(path); throw; }
        if(length==0){File.Delete(path);throw new InvalidDataException("不能上传空文件。");}
        return new StoredPlanUpload(safeName,relative,string.IsNullOrWhiteSpace(mimeType)?"application/octet-stream":mimeType,checked((uint)length),path);
    }

    private async Task<Stream?> TryOpenPublicAsync(FileCenterStoredFile file, CancellationToken token)
    {
        var candidates = BuildPublicCandidates(configuration["FileCenter:PublicBaseUrl"], file.StoragePath, file.SourceKind);
        if (candidates.Count == 0) return null;

        var client = httpClientFactory.CreateClient("FileCenterPublicFiles");
        foreach (var candidate in candidates)
        {
            using var response = await client.GetAsync(candidate, HttpCompletionOption.ResponseHeadersRead, token);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) continue;
            response.EnsureSuccessStatusCode();
            const long maximumBytes = 200L * 1024 * 1024;
            if (response.Content.Headers.ContentLength > maximumBytes) throw new InvalidDataException("公开文件超过预览大小限制。");
            await using var source = await response.Content.ReadAsStreamAsync(token);
            var output = new MemoryStream(response.Content.Headers.ContentLength is > 0 and <= int.MaxValue
                ? (int)response.Content.Headers.ContentLength.Value : 0);
            await source.CopyToAsync(output, token);
            if (output.Length > maximumBytes) { output.Dispose(); throw new InvalidDataException("公开文件超过预览大小限制。"); }
            output.Position = 0;
            return output;
        }
        return null;
    }

    public static IReadOnlyList<Uri> BuildPublicCandidates(string? baseUrl, string storagePath, string sourceKind)
    {
        var relative = NormalizeRelative(storagePath);
        if(sourceKind=="plan"&&relative.StartsWith("managed/",StringComparison.OrdinalIgnoreCase))return [];
        if (sourceKind == "request" && RequestPathPattern.IsMatch(relative)) return [];
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var origin) || origin.Scheme != Uri.UriSchemeHttps ||
            (origin.Host != "test.eggracks.com" && origin.Host != "www.eggracks.com"))
            throw new InvalidOperationException("文件中心公开站点配置无效。");

        var paths = new List<string>();
        if (relative.StartsWith("u_file/", StringComparison.OrdinalIgnoreCase)) paths.Add(relative);
        else
        {
            paths.Add("u_file/" + relative);
            if (sourceKind == "plan") paths.Add("u_file/purchase_plans/" + relative);
        }
        return paths.Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => new Uri(origin, string.Join('/', path.Split('/').Select(Uri.EscapeDataString))))
            .ToArray();
    }
    private string ResolveSiteRoot()
    {
        var configured = configuration["FileCenter:LegacySiteRoot"];
        if (string.IsNullOrWhiteSpace(configured)) throw new FileNotFoundException("尚未配置文件存储目录。");
        var path = Path.IsPathRooted(configured) ? configured : Path.Combine(environment.ContentRootPath, configured);
        path = Path.GetFullPath(path);
        if (!Directory.Exists(path)) throw new FileNotFoundException("文件存储目录不可用。", path);
        return path;
    }

    private string ResolvePlanUploadRoot()
    {
        var configured=configuration["FileCenter:PlanUploadRoot"];
        if(string.IsNullOrWhiteSpace(configured))configured="App_Data/file-center/plans";
        var path=Path.IsPathRooted(configured)?configured:Path.Combine(environment.ContentRootPath,configured);
        path=Path.GetFullPath(path);Directory.CreateDirectory(path);return path;
    }

    private static async Task<Stream> OpenEncryptedRequestAsync(string siteRoot, FileCenterStoredFile file, CancellationToken token)
    {
        var relative = file.StoragePath.Replace('\\', '/');
        if (!RequestPathPattern.IsMatch(relative)) throw new FileNotFoundException("客户文件路径无效。");
        var storageRoot = Path.GetFullPath(Path.Combine(siteRoot, "u_file", "purchase_requests"));
        var path = ResolveInside(storageRoot, relative);
        var keyPath = ResolveInside(siteRoot, "inc/file/purchase_request_storage_key.php");
        if (!File.Exists(path) || !File.Exists(keyPath)) throw new FileNotFoundException("客户文件不可用。");

        var blob = await File.ReadAllBytesAsync(path, token);
        if (blob.Length < 68 || !blob.AsSpan(0, 4).SequenceEqual("PRF1"u8)) throw new InvalidDataException("客户文件已损坏。");
        var keyText = await File.ReadAllTextAsync(keyPath, token);
        var match = Regex.Match(keyText, "\\A<\\?php exit; \\?>\\r?\\n([0-9a-f]{64})\\z", RegexOptions.CultureInvariant);
        if (!match.Success) throw new InvalidDataException("客户文件密钥格式无效。");
        var masterKey = Convert.FromHexString(match.Groups[1].Value);
        var iv = blob.AsSpan(4, 16).ToArray();
        var storedMac = blob.AsSpan(20, 32).ToArray();
        var cipher = blob.AsSpan(52).ToArray();

        var authenticationKey = HMACSHA256.HashData(masterKey, "authentication"u8);
        var signed = new byte[20 + cipher.Length];
        "PRF1"u8.CopyTo(signed);
        iv.CopyTo(signed, 4);
        cipher.CopyTo(signed, 20);
        var expectedMac = HMACSHA256.HashData(authenticationKey, signed);
        if (!CryptographicOperations.FixedTimeEquals(storedMac, expectedMac)) throw new InvalidDataException("客户文件完整性校验失败。");

        var encryptionKey = HMACSHA256.HashData(masterKey, "encryption"u8);
        using var aes = Aes.Create();
        aes.Key = encryptionKey; aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
        using var decryptor = aes.CreateDecryptor();
        var plaintext = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        if (plaintext.LongLength != file.FileSize) throw new InvalidDataException("客户文件长度校验失败。");
        return new MemoryStream(plaintext, writable: false);
    }

    private Stream OpenPlanFile(FileCenterStoredFile file)
    {
        var relative = NormalizeRelative(file.StoragePath);
        if(relative.StartsWith("managed/",StringComparison.OrdinalIgnoreCase))
        {
            var managedRelative=relative["managed/".Length..];
            var managedRoot=ResolvePlanUploadRoot();
            var managedPath=ResolveInside(managedRoot,managedRelative);
            if(!File.Exists(managedPath))throw new FileNotFoundException("内部文件不可用。");
            var managedInfo=new FileInfo(managedPath);
            if(managedInfo.Length!=file.FileSize)throw new InvalidDataException("内部文件长度校验失败。");
            return new FileStream(managedPath,FileMode.Open,FileAccess.Read,FileShare.Read,64*1024,FileOptions.Asynchronous|FileOptions.SequentialScan);
        }
        var siteRoot=ResolveSiteRoot();
        var uploadRoot = Path.GetFullPath(Path.Combine(siteRoot, "u_file"));
        var planRoot = Path.GetFullPath(Path.Combine(uploadRoot, "purchase_plans"));
        var candidates = new List<(string Root, string Relative)>();
        if (relative.StartsWith("u_file/", StringComparison.OrdinalIgnoreCase)) candidates.Add((siteRoot, relative));
        else { candidates.Add((planRoot, relative)); candidates.Add((uploadRoot, relative)); }

        foreach (var candidate in candidates)
        {
            string path;
            try { path = ResolveInside(candidate.Root, candidate.Relative); }
            catch (FileNotFoundException) { continue; }
            if (!File.Exists(path)) continue;
            var info = new FileInfo(path);
            if (info.Length != file.FileSize) throw new InvalidDataException("内部文件长度校验失败。");
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        }
        throw new FileNotFoundException("内部文件不可用。");
    }

    private static string NormalizeRelative(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 500 || value.IndexOf('\0') >= 0) throw new FileNotFoundException("文件路径无效。");
        var relative = value.Trim().Replace('\\', '/').TrimStart('/');
        if (Path.IsPathRooted(relative) || relative.Contains(':') || relative.Split('/').Any(part => part is ".." or "." or "")) throw new FileNotFoundException("文件路径无效。");
        return relative;
    }

    private static string ResolveInside(string root, string relative)
    {
        root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new FileNotFoundException("文件路径越界。");
        return path;
    }
}

public sealed record StoredPlanUpload(string OriginalName,string StoragePath,string MimeType,uint FileSize,string PhysicalPath);
