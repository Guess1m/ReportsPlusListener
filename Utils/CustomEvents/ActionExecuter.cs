using System;
using System.Reflection;
using ReportsPlus.Utils.Logging;

namespace ReportsPlus.Utils.CustomEvents{
    public static class ActionExecutor{
        public static void Execute(CustomActionConfig action)
        {
            try
            {
                Logger.LogInfo($"[ActionExecutor] Request received for: '{action.Name}'");

                if (string.IsNullOrEmpty(action.Target))
                {
                    Logger.LogError("[ActionExecutor] FAILED: Target is null or empty.");
                    return;
                }

                // 1. Parse Target String
                var lastDot = action.Target.LastIndexOf('.');
                if (lastDot == -1)
                {
                    Logger.LogError($"[ActionExecutor] Invalid Target format '{action.Target}'. Expected 'Namespace.Class.Method'");
                    return;
                }

                var typeName   = action.Target.Substring(0, lastDot);
                var methodName = action.Target.Substring(lastDot + 1);

                Logger.LogInfo($"[ActionExecutor] Looking for Type: '{typeName}' | Method: '{methodName}'");

                // 2. Find the Class (would be target type)
                var type = FindTypeInAssemblies(typeName);
                if (type == null)
                {
                    Logger.LogError($"[ActionExecutor] Target Class '{typeName}' NOT found in any loaded assembly.");
                    return;
                }

                Logger.LogInfo($"[ActionExecutor] Found Type: {type.FullName} in {type.Assembly.GetName().Name}");

                // Check Params
                object[] finalArgs = null;
                var      argTypes  = Type.EmptyTypes;

                if (action.Parameters != null && action.Parameters.Count > 0)
                {
                    finalArgs = new object[action.Parameters.Count];
                    argTypes  = new Type[action.Parameters.Count];

                    for (var i = 0; i < action.Parameters.Count; i++)
                    {
                        var paramConfig = action.Parameters[i];

                        // Find Parameter Type
                        var pType = FindTypeInAssemblies(paramConfig.Type);

                        if (pType == null)
                        {
                            Logger.LogError($"[ActionExecutor] Unknown parameter type '{paramConfig.Type}' (Param #{i + 1})");
                            return;
                        }

                        argTypes[i] = pType;

                        try
                        {
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

                // 4. Find Method
                var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.InvokeMethod, null, argTypes, null);

                if (method == null)
                {
                    Logger.LogError($"[ActionExecutor] Method '{methodName}' not found in '{typeName}' with the specific parameters provided.");
                    Logger.LogError("[ActionExecutor] Ensure your parameter types match EXACTLY.");
                    return;
                }

                // 5. Attempt method invoke
                Logger.LogInfo($"[ActionExecutor] Invoking: {typeName}.{methodName}...");

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
                if (ex.InnerException != null) Logger.LogError($"[ActionExecutor] Inner: {ex.InnerException.Message}");
            }
        }

        // Helper to find types even if they are in other DLLs
        private static Type FindTypeInAssemblies(string typeName)
        {
            // Try standard lookup first
            var type = Type.GetType(typeName);
            if (type != null) return type;

            // Scan all loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                try
                {
                    // Basic check to skip dynamic assemblies that crash on GetType
                    if (assembly.IsDynamic) continue;

                    type = assembly.GetType(typeName);
                    if (type != null) return type;
                }
                catch
                {
                    // Ignore assembly load errors
                    continue;
                }

            return null;
        }
    }
}