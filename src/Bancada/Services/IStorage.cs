using Microsoft.JSInterop;

namespace Bancada.Services;

public interface IStorage
{
    string? Get(string key);
    void Set(string key, string value);
}

public class MemoryStorage : IStorage
{
    private readonly Dictionary<string, string> _d = new();
    public string? Get(string key) => _d.GetValueOrDefault(key);
    public void Set(string key, string value) => _d[key] = value;
}

// Synchronous browser localStorage (WebAssembly runs in-process, so no async needed).
public class BrowserStorage(IJSRuntime js) : IStorage
{
    private readonly IJSInProcessRuntime _js = (IJSInProcessRuntime)js;
    public string? Get(string key) => _js.Invoke<string?>("localStorage.getItem", key);
    public void Set(string key, string value) => _js.InvokeVoid("localStorage.setItem", key, value);
}
