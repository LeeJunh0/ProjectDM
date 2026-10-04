using System;
using System.Collections.Generic;
using System.Reflection;

namespace ProjectDM.Editor
{
    // Test-only reflection routing: production systems never look up dependencies by name.
    internal static class RuntimeTestAccess
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static IEnumerable<object> Owners(object target)
        {
            yield return target;
            if (!(target is GameManager)) yield break;
            foreach (FieldInfo field in typeof(GameManager).GetFields(Flags))
            {
                if (field.FieldType.Namespace != "ProjectDM" || field.FieldType == typeof(GameManager)) continue;
                object owner = field.GetValue(target);
                if (owner != null) yield return owner;
            }
        }

        internal static object Get(object target, string name)
        {
            foreach (object owner in Owners(target))
            {
                FieldInfo field = owner.GetType().GetField(name, Flags);
                if (field != null) return field.GetValue(owner);
                PropertyInfo property = owner.GetType().GetProperty(name, Flags);
                if (property != null) return property.GetValue(owner);
            }
            throw new MissingMemberException(target.GetType().Name, name);
        }

        internal static void Set(object target, string name, object value)
        {
            bool found = false;
            foreach (object owner in Owners(target))
            {
                FieldInfo field = owner.GetType().GetField(name, Flags);
                if (field == null) continue;
                field.SetValue(owner, value);
                found = true;
                // Also keep the settings copy in sync in fixtures that do not execute Update.
                if (!(target is GameManager) || !(owner is GameManager)) break;
            }
            if (!found) throw new MissingMemberException(target.GetType().Name, name);
        }

        internal static object Call(object target, string name, params object[] args)
        {
            if (name == "ResetPrototypeRun") name = "ResetWorld";
            foreach (object owner in Owners(target))
            {
                MethodInfo method = owner.GetType().GetMethod(name, Flags);
                if (method != null) return method.Invoke(owner, args);
            }
            throw new MissingMethodException(target.GetType().Name, name);
        }
    }
}
