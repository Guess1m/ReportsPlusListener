using System;
using System.Linq;
using System.Reflection;
using ReportsPlus.Utils.Logging;

namespace ReportsPlus.Utils.CustomEvents{
    public static class ActionExecutor{
        public static void ExecuteTarget(CustomActionConfig action)
        {
            try
            {
                // 1. Find the Assembly (DLL)
                // We look through all loaded assemblies in the current AppDomain (LSPDFR)
                var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name.Equals(action.Assembly, StringComparison.OrdinalIgnoreCase));

                if (assembly == null)
                {
                    Logger.LogError($"ActionExecutor: Could not find plugin DLL named '{action.Assembly}'. Is it installed?");
                    return;
                }

                // 2. Parse the Target to get Class and Method
                // Target format expected: "Namespace.ClassName.MethodName"
                var lastDotIndex = action.Target.LastIndexOf('.');
                if (lastDotIndex == -1)
                {
                    Logger.LogError($"ActionExecutor: Invalid Target format '{action.Target}'. Expected 'Namespace.Class.Method'");
                    return;
                }

                var typeName   = action.Target.Substring(0, lastDotIndex);
                var methodName = action.Target.Substring(lastDotIndex + 1);

                // 3. Get the Type (Class)
                var type = assembly.GetType(typeName);
                if (type == null)
                {
                    Logger.LogError($"ActionExecutor: Could not find class '{typeName}' in {action.Assembly}");
                    return;
                }

                // 4. Get the Method
                var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.InvokeMethod);

                if (method == null)
                {
                    Logger.LogError($"ActionExecutor: Could not find method '{methodName}' in class '{typeName}'");
                    return;
                }

                // 5. Invoke the Method
                // Note: This assumes the method takes NO arguments (void).
                // If the target method requires parameters, this gets much more complex.
                Logger.LogInfo($"Invoking external action: {action.Target}");
                method.Invoke(null, null);
            }
            catch (Exception ex)
            {
                Logger.LogError($"ActionExecutor: Error invoking {action.Target}. Error: {ex.Message}");
            }
        }
    }
}