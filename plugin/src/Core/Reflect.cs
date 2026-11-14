using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace PeakEngine.Core
{
    /// <summary>
    /// Thrown when the game does not have a class/field/method we expected.
    /// This usually means PEAK was updated and something was renamed.
    /// </summary>
    public class MissingGameMemberException : Exception
    {
        public MissingGameMemberException(string message) : base(message) { }
    }

    /// <summary>
    /// Tiny helpers for "reflection": looking things up by NAME at runtime instead of at compile time.
    ///
    /// Why do it this way? Normally a mod is compiled against the game's own DLL (Assembly-CSharp.dll),
    /// so you can just write <c>character.data.currentStamina</c>. We look members up by name instead, so:
    ///   1. the plugin builds on any machine, even one without the game installed, and
    ///   2. if a game update renames something, only that one feature breaks (with a clear
    ///      error message) instead of the whole plugin refusing to load.
    ///
    /// The JavaScript equivalent of <c>Reflect.Get(obj, "data.currentStamina")</c> is
    /// <c>obj["data"]["currentStamina"]</c>.
    /// </summary>
    public static class Reflect
    {
        private const BindingFlags Everything =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        private static readonly Dictionary<string, Type> TypeCache = new Dictionary<string, Type>();

        /// <summary>The DLLs that hold PEAK's own code and its networking library.</summary>
        private static readonly string[] KnownAssemblies = { "Assembly-CSharp", "PhotonUnityNetworking", "PhotonRealtime" };

        // ---- Types -------------------------------------------------------------------------------

        /// <summary>Find a class by its full name in any loaded DLL. Returns null if it does not exist.</summary>
        public static Type FindType(string fullName)
        {
            lock (TypeCache)
            {
                if (TypeCache.TryGetValue(fullName, out var cached)) return cached;
                Type found = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try { found = assembly.GetType(fullName, false); }
                    catch { /* some dynamic assemblies throw; ignore them */ }
                    if (found != null) break;
                }
                // Not loaded yet? Ask .NET to load the DLLs the game keeps its code in.
                foreach (var assemblyName in KnownAssemblies)
                {
                    if (found != null) break;
                    try { found = Type.GetType(fullName + ", " + assemblyName, false); }
                    catch { /* that DLL does not exist in this game; fine */ }
                }
                // Only remember hits: a DLL that loads later could still provide the type.
                if (found != null) TypeCache[fullName] = found;
                return found;
            }
        }

        /// <summary>Like <see cref="FindType"/>, but throws a readable error when the class is missing.</summary>
        public static Type RequireType(string fullName)
        {
            return FindType(fullName)
                ?? throw new MissingGameMemberException($"The game has no class named '{fullName}'. PEAK may have been updated.");
        }

        // ---- Fields and properties ---------------------------------------------------------------

        /// <summary>Find a field or property by name, also searching parent classes. Null if missing.</summary>
        public static MemberInfo FindMember(Type type, string name)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var field = t.GetField(name, Everything);
                if (field != null) return field;
                var property = t.GetProperty(name, Everything);
                if (property != null) return property;
            }
            return null;
        }

        public static bool HasMember(Type type, string name) => type != null && FindMember(type, name) != null;

        /// <summary>The type a field/property holds, e.g. MemberType(Character, "data") is CharacterData.</summary>
        public static Type MemberType(Type type, string name)
        {
            var member = type == null ? null : FindMember(type, name);
            if (member is FieldInfo f) return f.FieldType;
            if (member is PropertyInfo p) return p.PropertyType;
            return null;
        }

        /// <summary>
        /// Read a value by path. <c>Get(character, "data.currentStamina")</c> reads character.data.currentStamina.
        /// Throws if a name along the path does not exist or a value along the way is null.
        /// </summary>
        public static object Get(object target, string path)
        {
            if (target == null) throw new ArgumentNullException(nameof(target), $"Cannot read '{path}' from null.");
            object current = target;
            foreach (var name in path.Split('.'))
            {
                if (current == null)
                    throw new MissingGameMemberException($"'{name}' could not be read because the value before it in '{path}' is null.");
                current = ReadMember(current.GetType(), current, name);
            }
            return current;
        }

        /// <summary>Same as <see cref="Get"/> but for static members: <c>GetStatic(characterType, "localCharacter")</c>.</summary>
        public static object GetStatic(Type type, string path)
        {
            var names = path.Split('.');
            object current = ReadMember(type, null, names[0]);
            for (int i = 1; i < names.Length; i++)
            {
                if (current == null)
                    throw new MissingGameMemberException($"'{names[i]}' could not be read because the value before it in '{type.Name}.{path}' is null.");
                current = ReadMember(current.GetType(), current, names[i]);
            }
            return current;
        }

        /// <summary>Like <see cref="Get"/>, but returns <paramref name="fallback"/> instead of throwing.</summary>
        public static T GetOr<T>(object target, string path, T fallback)
        {
            try
            {
                if (target == null) return fallback;
                var value = Get(target, path);
                return value == null ? fallback : (T)ConvertTo(value, typeof(T));
            }
            catch { return fallback; }
        }

        /// <summary>Write a value by path: <c>Set(character, "data.isInvincible", true)</c>.</summary>
        public static void Set(object target, string path, object value)
        {
            if (target == null) throw new ArgumentNullException(nameof(target), $"Cannot set '{path}' on null.");
            int lastDot = path.LastIndexOf('.');
            object owner = lastDot < 0 ? target : Get(target, path.Substring(0, lastDot));
            string name = lastDot < 0 ? path : path.Substring(lastDot + 1);
            if (owner == null) throw new MissingGameMemberException($"Cannot set '{path}': the object that owns '{name}' is null.");

            var member = FindMember(owner.GetType(), name)
                ?? throw new MissingGameMemberException(Missing(owner.GetType(), name));
            if (member is FieldInfo field) field.SetValue(owner, ConvertTo(value, field.FieldType));
            else
            {
                var property = (PropertyInfo)member;
                if (!property.CanWrite) throw new MissingGameMemberException($"'{owner.GetType().Name}.{name}' is read-only.");
                property.SetValue(owner, ConvertTo(value, property.PropertyType), null);
            }
        }

        private static object ReadMember(Type type, object instance, string name)
        {
            var member = FindMember(type, name) ?? throw new MissingGameMemberException(Missing(type, name));
            try
            {
                if (member is FieldInfo field) return field.GetValue(field.IsStatic ? null : instance);
                var property = (PropertyInfo)member;
                return property.GetValue(property.GetGetMethod(true).IsStatic ? null : instance, null);
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                throw e.InnerException;
            }
        }

        // ---- Methods -----------------------------------------------------------------------------

        public static bool HasMethod(Type type, string name) => type != null && FindMethods(type, name).Count > 0;

        /// <summary>All methods with this name on the class and its parents.</summary>
        public static List<MethodInfo> FindMethods(Type type, string name)
        {
            var found = new List<MethodInfo>();
            for (var t = type; t != null; t = t.BaseType)
                found.AddRange(t.GetMethods(Everything).Where(m => m.Name == name));
            return found;
        }

        /// <summary>
        /// Call a method by name: <c>Call(afflictions, "SetStatus", statusType, 0.5f)</c>.
        ///
        /// Game updates often ADD optional parameters to methods. To survive that, you only pass the
        /// leading arguments you care about; any remaining parameters are filled with their default values.
        /// </summary>
        public static object Call(object target, string method, params object[] args)
        {
            if (target == null) throw new ArgumentNullException(nameof(target), $"Cannot call '{method}' on null.");
            return Invoke(target.GetType(), target, method, args);
        }

        /// <summary>Same as <see cref="Call"/> for static methods: <c>CallStatic(photonNetworkType, "Instantiate", ...)</c>.</summary>
        public static object CallStatic(Type type, string method, params object[] args)
        {
            return Invoke(type, null, method, args);
        }

        private static object Invoke(Type type, object instance, string method, object[] args)
        {
            args = args ?? new object[0];
            var candidates = FindMethods(type, method);
            if (candidates.Count == 0) throw new MissingGameMemberException(Missing(type, method + "()"));

            MethodInfo best = null;
            object[] bestArgs = null;
            foreach (var candidate in candidates)
            {
                var bound = TryBind(candidate, args);
                if (bound == null) continue;
                // Prefer the overload that needs the fewest filled-in defaults.
                if (best == null || candidate.GetParameters().Length < best.GetParameters().Length)
                {
                    best = candidate;
                    bestArgs = bound;
                }
            }

            if (best == null)
            {
                var shapes = string.Join(" | ", candidates.Select(Describe));
                throw new MissingGameMemberException(
                    $"'{type.Name}.{method}' exists but does not accept {args.Length} argument(s) of the types we pass. " +
                    $"The game's version looks like: {shapes}");
            }

            try
            {
                return best.Invoke(best.IsStatic ? null : instance, bestArgs);
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                // Reflection wraps the real error; unwrap it so logs show what actually went wrong.
                throw e.InnerException;
            }
        }

        /// <summary>Try to line our arguments up with a method's parameters. Null means "does not fit".</summary>
        private static object[] TryBind(MethodInfo method, object[] args)
        {
            var parameters = method.GetParameters();
            if (args.Length > parameters.Length) return null;

            var bound = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                if (i < args.Length)
                {
                    if (!CanConvert(args[i], parameter.ParameterType)) return null;
                    bound[i] = ConvertTo(args[i], parameter.ParameterType);
                }
                else if (parameter.HasDefaultValue)
                {
                    bound[i] = parameter.DefaultValue;
                }
                else if (parameter.ParameterType.Name == "PhotonMessageInfo")
                {
                    // Network methods get an extra "who sent this" struct appended by Photon.
                    bound[i] = Activator.CreateInstance(parameter.ParameterType);
                }
                else
                {
                    return null; // a required parameter we were not given
                }
            }
            return bound;
        }

        /// <summary>
        /// The parameters of a method, ignoring Photon's trailing PhotonMessageInfo.
        /// Used to send network calls with exactly the number of arguments this game version expects.
        /// </summary>
        public static ParameterInfo[] RpcParameters(Type type, string method)
        {
            var found = FindMethods(type, method).FirstOrDefault();
            if (found == null) return null;
            return found.GetParameters().Where(p => p.ParameterType.Name != "PhotonMessageInfo").ToArray();
        }

        // ---- Value conversion --------------------------------------------------------------------

        private static bool CanConvert(object value, Type target)
        {
            if (value == null) return !target.IsValueType || Nullable.GetUnderlyingType(target) != null;
            var source = value.GetType();
            if (target.IsAssignableFrom(source)) return true;
            if (target.IsEnum) return value is string || IsNumber(source);
            if (target == typeof(bool) || source == typeof(bool)) return false;
            return IsNumber(source) && IsNumber(target);
        }

        /// <summary>Convert a value to the type a field/parameter wants (e.g. double to float, "Hunger" to an enum).</summary>
        public static object ConvertTo(object value, Type target)
        {
            if (value == null) return null;
            var source = value.GetType();
            if (target.IsAssignableFrom(source)) return value;
            if (target.IsEnum)
            {
                if (value is string text) return Enum.Parse(target, text, true);
                return Enum.ToObject(target, value);
            }
            return Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
        }

        private static bool IsNumber(Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte: case TypeCode.SByte: case TypeCode.Int16: case TypeCode.UInt16:
                case TypeCode.Int32: case TypeCode.UInt32: case TypeCode.Int64: case TypeCode.UInt64:
                case TypeCode.Single: case TypeCode.Double: case TypeCode.Decimal:
                    return !type.IsEnum;
                default:
                    return false;
            }
        }

        // ---- Error text --------------------------------------------------------------------------

        private static string Missing(Type type, string name) =>
            $"The game's '{type.Name}' has no member called '{name}'. PEAK may have been updated and renamed it.";

        private static string Describe(MethodInfo method) =>
            method.Name + "(" + string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")";
    }
}
