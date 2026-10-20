using System;
using System.Collections.Generic;
using System.Linq;

namespace PCL.Core.App.IoC;

public static partial class DependencyGroups
{
    private static readonly AttributeTargets[] _TargetsToTry = [
        AttributeTargets.Method,
        AttributeTargets.Property,
        AttributeTargets.Class
    ];
    
    public static bool InvokeInjection(Delegate injection, string identifier, AttributeTargets targets)
    {
        // 该标识符下没有任何依赖被注册时视为“空注入”，不算失败
        // （否则声明了注入点但暂无实现的组件会在启动时刷出误导性的 WARN）
        if (!_GroupMap.TryGetValue(identifier, out var groups)) return true;
        var targetGroups = new List<DependencyGroup>();
        foreach (var targetToTry in _TargetsToTry.Where(t => targets.HasFlag(t)))
        {
            // 该目标类型下没有依赖时跳过，而不是判定整次注入失败
            if (groups.GetValueOrDefault(targetToTry) is not { } group) continue;
            targetGroups.Add(group);
        }
        foreach (var group in targetGroups) group.InvokeInjection(injection);
        return true;
    }
}
