using System;
using System.Reflection;
using ReportsPlus.Utils.Logging;

namespace ReportsPlus.Utils.CustomEvents
{
    /// <summary>
    ///     Provides functionality to dynamically find and execute methods across loaded assemblies.
    /// </summary>
    public static class ActionExecutor
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, MethodInfo> MethodCache = new System.Collections.Concurrent.ConcurrentDictionary<string, MethodInfo>();

        /// <summary>
        ///     Dynamically resolves a method target and its parameters across all loaded assemblies and invokes it.
        ///     Supports both static and instance methods with primitive or enum parameter conversion.
        /// </summary>
        /// <param name="action">The <see cref="CustomActionConfig" /> containing the reflection target and parameter values.</param>
        public static void Execute(CustomActionConfig action)
        {
            try
            {
                if (action == null || string.IsNullOrWhiteSpace(action.Target))
                {
                    Logger.LogError("[ActionExecutor] Action configuration or target is null/empty.");
                    return;
                }

                Logger.LogInfo($"[ActionExecutor] Request received for: '{action.Name}'");

                if (!MethodCache.TryGetValue(action.Target, out var method))
                {
                    var lastDot = action.Target.LastIndexOf('.');
                    if (lastDot == -1)
                    {
                        Logger.LogError($"[ActionExecutor] Invalid Target format '{action.Target}'. Expected 'Namespace.Class.Method'");
                        return;
                    }

                    var typeName = action.Target.Substring(0, lastDot);
                    var methodName = action.Target.Substring(lastDot + 1);

                    if (string.IsNullOrWhiteSpace(typeName) || string.IsNullOrWhiteSpace(methodName))
                    {
                        Logger.LogError("[ActionExecutor] Target format is invalid. Missing Type or Method.");
                        return;
                    }

                    var type = FindTypeInAssemblies(typeName);
                    if (type == null)
                    {
                        Logger.LogError($"[ActionExecutor] Target Class '{typeName}' NOT found in any loaded assembly.");
                        return;
                    }

                    var paramTypes = Type.EmptyTypes;
                    if (action.Parameters != null && action.Parameters.Count > 0)
                    {
                        paramTypes = new Type[action.Parameters.Count];
                        for (var i = 0; i < action.Parameters.Count; i++)
                        {
                            var paramConfig = action.Parameters[i];
                            if (paramConfig == null || string.IsNullOrWhiteSpace(paramConfig.Type))
                            {
                                Logger.LogError($"[ActionExecutor] Parameter configuration at index {i} is invalid.");
                                return;
                            }

                            var pType = FindTypeInAssemblies(paramConfig.Type);
                            if (pType == null)
                            {
                                Logger.LogError($"[ActionExecutor] Unknown parameter type '{paramConfig.Type}'");
                                return;
                            }
                            paramTypes[i] = pType;
                        }
                    }

                    method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.InvokeMethod, null, paramTypes, null);

                    if (method == null)
                    {
                        Logger.LogError($"[ActionExecutor] Method '{methodName}' not found in '{typeName}' with the specific parameters provided.");
                        return;
                    }

                    MethodCache[action.Target] = method;
                }

                object[] finalArgs = null;
                if (action.Parameters != null && action.Parameters.Count > 0)
                {
                    finalArgs = new object[action.Parameters.Count];
                    var parametersInfos = method.GetParameters();

                    for (var i = 0; i < action.Parameters.Count; i++)
                    {
                        var paramConfig = action.Parameters[i];
                        if (paramConfig == null || string.IsNullOrWhiteSpace(paramConfig.Value))
                        {
                            continue;
                        }

                        var targetType = parametersInfos[i].ParameterType;

                        try
                        {
                            if (targetType.IsEnum)
                            {
                                finalArgs[i] = Enum.Parse(targetType, paramConfig.Value, true);
                            }
                            else
                            {
                                finalArgs[i] = Convert.ChangeType(paramConfig.Value, targetType);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"[ActionExecutor] Conversion failed for value '{paramConfig.Value}' to type '{targetType.Name}'. Error: {ex.Message}");
                            return;
                        }
                    }
                }

                try
                {
                    method.Invoke(null, finalArgs);
                    Logger.LogInfo("[ActionExecutor] Invocation successful.");
                }
                catch (TargetInvocationException tie)
                {
                    Logger.LogError($"[ActionExecutor] External Plugin Error: {tie.InnerException?.Message ?? tie.Message}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[ActionExecutor] CRITICAL ERROR: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Logger.LogError($"[ActionExecutor] Inner: {ex.InnerException.Message}");
                }
            }
        }

        /// <summary>
        ///     Iterates through all loaded application domains to locate a <see cref="Type" /> matching the provided fully
        ///     qualified name.
        /// </summary>
        /// <param name="typeName">The full name of the type, including namespace.</param>
        /// <returns>The resolved <see cref="Type" />, or null if no matching type could be found in non-dynamic assemblies.</returns>
        private static Type FindTypeInAssemblies(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return null;

            var type = Type.GetType(typeName);
            if (type != null) return type;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (assembly.IsDynamic) continue; // skip dynamic
                    type = assembly.GetType(typeName);
                    if (type != null) return type;
                }
                catch
                {
                    // catch thrown by assembly
                }
            }

            return null;
        }
    }
}