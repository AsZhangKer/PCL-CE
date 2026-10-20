using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace PCL.Core.App.Configuration.Storage;

public class DynamicCacheConfigStorage : ConfigStorage
{
    private readonly Dictionary<object, ConfigStorage> _cache = [];

    // 该字典不是线程安全的，而配置存取会同时发生在 UI 线程与线程池上
    //（实例列表扫描、批量保存实例配置、下载任务等），并发写入会直接破坏 Dictionary 内部状态，
    // 进而在 ConfigStorage.Access 中被当作致命错误强制关闭程序。
    // 因此所有对 _cache / _nullContextCache 的读写都必须经由该锁串行化。
    private readonly object _cacheLock = new();

    private ConfigStorage? _nullContextCache;

    /// <summary>
    /// 存取仓库工厂。在没有匹配的上下文实例时将被调用，以创建新的上下文实例。
    /// </summary>
    public required Func<object?, ConfigStorage> StorageFactory { get; init; }

    /// <summary>
    /// 取（或创建）指定上下文对应的存取仓库。整个查找 + 创建过程都在锁内完成，
    /// 以保证同一上下文只会创建一个实例，避免重复创建导致的句柄泄漏。
    /// </summary>
    private ConfigStorage GetStorage(object? context)
    {
        lock (_cacheLock)
        {
            if (context is null)
                return _nullContextCache ??= StorageFactory(null);
            if (!_cache.TryGetValue(context, out var cached))
                _cache[context] = cached = StorageFactory(context);
            return cached;
        }
    }

    protected override bool OnAccess<TKey, TValue>(StorageAction action, ref TKey key, [NotNullWhen(true)] ref TValue value, object? context)
    {
        ConfigStorage storage;
        try
        {
            storage = GetStorage(context);
        }
        catch (Exception ex)
        {
            throw new Exception("Failed to invoke storage factory", ex);
        }

        return storage.Access(action, ref key, ref value, context);
    }

    protected override void OnStop()
    {
        ConfigStorage[] storages;
        lock (_cacheLock)
        {
            storages = [.. _cache.Values];
            _cache.Clear();
            if (_nullContextCache is not null)
            {
                storages = [.. storages, _nullContextCache];
                _nullContextCache = null;
            }
        }

        // Stop 可能涉及文件写入，放在锁外执行，避免与其它线程的存取互相阻塞
        foreach (var item in storages) item.Stop();
    }

    public bool InvalidateCache(object context)
    {
        ConfigStorage? storage;
        lock (_cacheLock)
        {
            if (!_cache.TryGetValue(context, out storage)) return false;
            _cache.Remove(context);
        }

        storage.Stop();
        return true;
    }
}
