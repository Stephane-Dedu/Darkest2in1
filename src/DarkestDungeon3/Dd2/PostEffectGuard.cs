using System;
using System.Collections.Generic;
using System.Reflection;

namespace DarkestDungeon3.Dd2;

/// <summary>Hold selected URP fields without disabling unrelated fields such as exposure.
/// Reflection keeps the bridge independent of the game's URP version; values are restored after the fight.</summary>
internal sealed class PostEffectGuard
{
    private sealed class Held
    {
        public object Instance, Before, During;
        public FieldInfo Field;
        public PropertyInfo Property;
        public object Read() => Field != null ? Field.GetValue(Instance) : Property.GetValue(Instance);
        public void Write(object value)
        {
            if (Field != null) Field.SetValue(Instance, value);
            else Property.SetValue(Instance, value);
        }
    }
    private readonly Dictionary<object, Dictionary<string, Held>> _held = new();
    public int Count { get; private set; }
    public void Disable(object component) => Hold(component, "active", false);
    public void NeutralParameter(object component, string name, object neutral)
    {
        var parameter = component?.GetType().GetField(name)?.GetValue(component);
        if (parameter != null) Hold(parameter, "value", neutral);
    }
    private void Hold(object instance, string name, object during)
    {
        if (instance == null) return;
        if (!_held.TryGetValue(instance, out var members)) _held[instance] = members = new();
        if (members.ContainsKey(name)) return;
        var type = instance.GetType();
        var field = type.GetField(name);
        var property = field == null ? type.GetProperty(name) : null;
        if (field == null && (property == null || !property.CanRead || !property.CanWrite)) return;
        var held = new Held { Instance = instance, Field = field, Property = property, During = during };
        held.Before = held.Read();
        members[name] = held;
        Count++;
    }
    public void Apply()
    {
        foreach (var members in _held.Values)
            foreach (var held in members.Values)
                if (!Equals(held.Read(), held.During)) held.Write(held.During);
    }
    public void Restore()
    {
        foreach (var members in _held.Values)
            foreach (var held in members.Values) held.Write(held.Before);
        _held.Clear();
        Count = 0;
    }
}
