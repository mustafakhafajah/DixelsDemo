using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace Dixels.Portal.BlobStoring;

/* Tests keep BLOBs in memory instead of on disk, apart per tenant and container, like ABP's file-system provider. */
public class InMemoryBlobProvider : BlobProviderBase, ISingletonDependency
{
    private readonly ConcurrentDictionary<string, byte[]> _blobs = new();
    private readonly ICurrentTenant _currentTenant;

    public InMemoryBlobProvider(ICurrentTenant currentTenant)
    {
        _currentTenant = currentTenant;
    }

    public override async Task SaveAsync(BlobProviderSaveArgs args)
    {
        var key = KeyOf(args);
        if (!args.OverrideExisting && _blobs.ContainsKey(key))
            throw new BlobAlreadyExistsException($"'{args.BlobName}' already exists in '{args.ContainerName}'.");

        using var copy = new MemoryStream();
        await args.BlobStream.CopyToAsync(copy, args.CancellationToken);
        _blobs[key] = copy.ToArray();
    }

    public override Task<bool> DeleteAsync(BlobProviderDeleteArgs args) => Task.FromResult(_blobs.TryRemove(KeyOf(args), out _));

    public override Task<bool> ExistsAsync(BlobProviderExistsArgs args) => Task.FromResult(_blobs.ContainsKey(KeyOf(args)));

    public override Task<Stream?> GetOrNullAsync(BlobProviderGetArgs args)
        => Task.FromResult<Stream?>(_blobs.TryGetValue(KeyOf(args), out var bytes) ? new MemoryStream(bytes, writable: false) : null);

    private string KeyOf(BlobProviderArgs args)
        => $"{_currentTenant.Id?.ToString("D") ?? "host"}/{args.ContainerName}/{args.BlobName}";
}
