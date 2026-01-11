using System;
using System.Reflection;
using ReportsPlus.Utils.Logging;

namespace ReportsPlus.Utils.CustomEvents{
    /// <summary>
    ///     Provides functionality to dynamically find and execute methods across loaded assemblies.
    /// </summary>
    public static class ActionExecutor{
        /// <summary>
        ///     Dynamically resolves a method target and its parameters across all loaded assemblies and invokes it.
        ///     Supports both static and instance methods with primitive or enum parameter conversion.
        /// </summary>
        /// <param name="action">The <see cref="CustomActionConfig" /> containing the reflection target and parameter values.</param>
        public static void Execute(CustomActionConfig action)
        {
            try
            {
                if (action == null)
                {
                    Logger.LogError("[ActionExecutor] Action configuration is null.");
                    return;
                }

                Logger.LogInfo($"[ActionExecutor] Request received for: '{action.Name}'");

                if (string.IsNullOrEmpty(action.Target))
                {
                    Logger.LogError("[ActionExecutor] FAILED: Target is null or empty.");
                    return;
                }

                var lastDot = action.Target.LastIndexOf('.');
                if (lastDot == -1)
                {
                    Logger.LogError($"[ActionExecutor] Invalid Target format '{action.Target}'. Expected 'Namespace.Class.Method'");
                    return;
                }

                // split into class path and method name
                var typeName   = action.Target.Substring(0, lastDot);
                var methodName = action.Target.Substring(lastDot + 1);

                Logger.LogInfo($"[ActionExecutor] Looking for Type: '{typeName}' | Method: '{methodName}'");

                var type = FindTypeInAssemblies(typeName);
                if (type == null)
                {
                    Logger.LogError($"[ActionExecutor] Target Class '{typeName}' NOT found in any loaded assembly.");
                    return;
                }

                Logger.LogInfo($"[ActionExecutor] Found Type: {type.FullName} in {type.Assembly.GetName().Name}");

                object[] finalArgs = null;
                var      argTypes  = Type.EmptyTypes;

                if (action.Parameters != null && action.Parameters.Count > 0)
                {
                    finalArgs = new object[action.Parameters.Count];
                    argTypes  = new Type[action.Parameters.Count];

                    for (var i = 0; i < action.Parameters.Count; i++)
                    {
                        var paramConfig = action.Parameters[i];
                        if (paramConfig == null) continue;

                        var pType = FindTypeInAssemblies(paramConfig.Type);
                        if (pType == null)
                        {
                            Logger.LogError($"[ActionExecutor] Unknown parameter type '{paramConfig.Type}' (Param #{i + 1})");
                            return;
                        }

                        argTypes[i] = pType;

                        try
                        {
                            // Convert string to requested type
                            if (pType.IsEnum)
                                finalArgs[i] = Enum.Parse(pType, paramConfig.Value);
                            else
                                finalArgs[i] = Convert.ChangeType(paramConfig.Value, pType);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"[ActionExecutor] Conversion failed for value '{paramConfig.Value}' to type '{pType.Name}'. Error: {ex.Message}");
                            return;
                        }
                    }
                }

                // check for public static method matching name and sig
                var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.InvokeMethod, null, argTypes, null);

                if (method == null)
                {
                    Logger.LogError($"[ActionExecutor] Method '{methodName}' not found in '{typeName}' with the specific parameters provided.");
                    return;
                }

                Logger.LogInfo($"[ActionExecutor] Invoking: {typeName}.{methodName}...");

                try
                {
                    method.Invoke(null, finalArgs); // Assuming static method
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
                if (ex.InnerException != null) Logger.LogError($"[ActionExecutor] Inner: {ex.InnerException.Message}");
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