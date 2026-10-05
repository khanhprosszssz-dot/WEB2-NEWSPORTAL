using Microsoft.Extensions.Caching.Memory;
namespace NewsPortal.Api.Services;
public class PublicCache(IMemoryCache memory)
{
    private readonly string instance = Guid.NewGuid().ToString("N");
    private long generation;
    public void Invalidate() => Interlocked.Increment(ref generation);
    public string Key(string request) => $"public:{instance}:{Interlocked.Read(ref generation)}:{request}";
    public bool TryGet(string key, out object? value) => memory.TryGetValue(key, out value);
    public void Set(string key, object value) => memory.Set(key, value, TimeSpan.FromSeconds(30));
}
