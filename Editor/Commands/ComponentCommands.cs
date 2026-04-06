// Commands/ComponentCommands.cs
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

namespace Tykit
{
    [InitializeOnLoad]
    public static class ComponentCommands
    {
        static ComponentCommands()
        {
            RegisterCommands();
        }

        public static void RegisterCommands()
        {
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "add-component",
                    "Add a component to a GameObject.",
                    "scene.mutate",
                    true,
                    CommandSchema.Object(
                        ("id", CommandSchema.Integer("GameObject instanceId.")),
                        ("path", CommandSchema.String("Hierarchy path.")),
                        ("name", CommandSchema.String("GameObject name.")),
                        ("component", CommandSchema.String("Component type name.")))),
                AddComponent);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "remove-component",
                    "Remove a component from a GameObject.",
                    "scene.mutate",
                    true,
                    CommandSchema.Object(
                        ("id", CommandSchema.Integer("GameObject instanceId.")),
                        ("path", CommandSchema.String("Hierarchy path.")),
                        ("name", CommandSchema.String("GameObject name.")),
                        ("component", CommandSchema.String("Component type name.")))),
                RemoveComponent);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "get-properties",
                    "Read serialized properties from one or all components on a GameObject.",
                    "scene.query",
                    false,
                    CommandSchema.Object(
                        ("id", CommandSchema.Integer("GameObject instanceId.")),
                        ("path", CommandSchema.String("Hierarchy path.")),
                        ("name", CommandSchema.String("GameObject name.")),
                        ("component", CommandSchema.String("Optional component type name.")),
                        ("structured", CommandSchema.Boolean("If true, return native JSON types (numbers/arrays/objects) instead of strings. Default false for backward compat.")))),
                GetProperties);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "set-property",
                    "Set a serialized property on a component. Accepts scalars, arrays (Vector/Color/Rect/Quaternion/Bounds), and indexed paths (e.g. 'm_Items.Array.data[2]').",
                    "scene.mutate",
                    true,
                    CommandSchema.Object(
                        ("id", CommandSchema.Integer("GameObject instanceId.")),
                        ("path", CommandSchema.String("Hierarchy path.")),
                        ("name", CommandSchema.String("GameObject name.")),
                        ("component", CommandSchema.String("Component type name.")),
                        ("property", CommandSchema.String("Serialized property path.")),
                        ("value", new JObject { ["description"] = "JSON value (int/float/bool/string/array/object)." }))),
                SetProperty);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "array-size",
                    "Get or set the size of a serialized array/list property.",
                    "scene.mutate",
                    true,
                    CommandSchema.Object(
                        ("id", CommandSchema.Integer("GameObject instanceId.")),
                        ("path", CommandSchema.String("Hierarchy path.")),
                        ("name", CommandSchema.String("GameObject name.")),
                        ("component", CommandSchema.String("Component type name.")),
                        ("property", CommandSchema.String("Serialized array property path.")),
                        ("size", CommandSchema.Integer("Optional new size. Omit to only read current size.")))),
                ArraySize);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "array-insert",
                    "Insert a new element into a serialized array/list at the given index (default: append).",
                    "scene.mutate",
                    true,
                    CommandSchema.Object(
                        ("id", CommandSchema.Integer("GameObject instanceId.")),
                        ("path", CommandSchema.String("Hierarchy path.")),
                        ("name", CommandSchema.String("GameObject name.")),
                        ("component", CommandSchema.String("Component type name.")),
                        ("property", CommandSchema.String("Serialized array property path.")),
                        ("index", CommandSchema.Integer("Insertion index. Omit to append at end.")),
                        ("value", new JObject { ["description"] = "Optional value to assign to the new element." }))),
                ArrayInsert);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "array-delete",
                    "Delete an element from a serialized array/list at the given index.",
                    "scene.mutate",
                    true,
                    CommandSchema.Object(
                        ("id", CommandSchema.Integer("GameObject instanceId.")),
                        ("path", CommandSchema.String("Hierarchy path.")),
                        ("name", CommandSchema.String("GameObject name.")),
                        ("component", CommandSchema.String("Component type name.")),
                        ("property", CommandSchema.String("Serialized array property path.")),
                        ("index", CommandSchema.Integer("Index to delete.")))),
                ArrayDelete);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "set-text",
                    "Set the text on a TextMeshProUGUI/TMP_Text/Text component on the GameObject.",
                    "scene.mutate",
                    true,
                    CommandSchema.Object(
                        ("id", CommandSchema.Integer("GameObject instanceId.")),
                        ("path", CommandSchema.String("Hierarchy path.")),
                        ("name", CommandSchema.String("GameObject name.")),
                        ("text", CommandSchema.String("Text content to assign.")),
                        ("inChildren", CommandSchema.Boolean("If true, search children for the first text component. Default false.")))),
                SetText);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "array-move",
                    "Move an element inside a serialized array/list from one index to another.",
                    "scene.mutate",
                    true,
                    CommandSchema.Object(
                        ("id", CommandSchema.Integer("GameObject instanceId.")),
                        ("path", CommandSchema.String("Hierarchy path.")),
                        ("name", CommandSchema.String("GameObject name.")),
                        ("component", CommandSchema.String("Component type name.")),
                        ("property", CommandSchema.String("Serialized array property path.")),
                        ("fromIndex", CommandSchema.Integer("Source index.")),
                        ("toIndex", CommandSchema.Integer("Destination index.")))),
                ArrayMove);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "get-array",
                    "Read all elements of a serialized array/list property as a structured JSON array.",
                    "scene.query",
                    false,
                    CommandSchema.Object(
                        ("id", CommandSchema.Integer("GameObject instanceId.")),
                        ("path", CommandSchema.String("Hierarchy path.")),
                        ("name", CommandSchema.String("GameObject name.")),
                        ("component", CommandSchema.String("Component type name.")),
                        ("property", CommandSchema.String("Serialized array property path.")))),
                GetArray);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "call-method",
                    "Invoke a public (or non-public) method on a component via reflection. Returns the result as JSON.",
                    "scene.mutate",
                    true,
                    CommandSchema.Object(
                        ("id", CommandSchema.Integer("GameObject instanceId.")),
                        ("path", CommandSchema.String("Hierarchy path.")),
                        ("name", CommandSchema.String("GameObject name.")),
                        ("component", CommandSchema.String("Component type name.")),
                        ("method", CommandSchema.String("Method name.")),
                        ("params", CommandSchema.Array(new JObject(), "Optional parameter array.")),
                        ("nonPublic", CommandSchema.Boolean("Allow invoking non-public methods. Default false.")))),
                CallMethod);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "get-field",
                    "Read a field or property value on a component via reflection (bypasses SerializedProperty).",
                    "scene.query",
                    false,
                    CommandSchema.Object(
                        ("id", CommandSchema.Integer("GameObject instanceId.")),
                        ("path", CommandSchema.String("Hierarchy path.")),
                        ("name", CommandSchema.String("GameObject name.")),
                        ("component", CommandSchema.String("Component type name.")),
                        ("field", CommandSchema.String("Field or property name.")),
                        ("nonPublic", CommandSchema.Boolean("Allow accessing non-public members. Default true.")))),
                GetField);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "set-field",
                    "Write a field or property value on a component via reflection (bypasses SerializedProperty).",
                    "scene.mutate",
                    true,
                    CommandSchema.Object(
                        ("id", CommandSchema.Integer("GameObject instanceId.")),
                        ("path", CommandSchema.String("Hierarchy path.")),
                        ("name", CommandSchema.String("GameObject name.")),
                        ("component", CommandSchema.String("Component type name.")),
                        ("field", CommandSchema.String("Field or property name.")),
                        ("value", new JObject { ["description"] = "JSON value (int/float/bool/string/array/object)." }),
                        ("nonPublic", CommandSchema.Boolean("Allow accessing non-public members. Default true.")))),
                SetField);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "button-click",
                    "Simulate clicking a UI Button by invoking its onClick event.",
                    "scene.mutate",
                    true,
                    CommandSchema.Object(
                        ("id", CommandSchema.Integer("Button GameObject instanceId.")),
                        ("path", CommandSchema.String("Hierarchy path.")),
                        ("name", CommandSchema.String("GameObject name.")))),
                ButtonClick);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "component-copy",
                    "Copy a component's values to the internal clipboard. Use component-paste to paste onto another GameObject.",
                    "scene.mutate",
                    true,
                    CommandSchema.Object(
                        ("id", CommandSchema.Integer("GameObject instanceId.")),
                        ("path", CommandSchema.String("Hierarchy path.")),
                        ("name", CommandSchema.String("GameObject name.")),
                        ("component", CommandSchema.String("Component type name to copy.")))),
                ComponentCopy);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "component-paste",
                    "Paste previously copied component values onto this GameObject. Uses 'asNew' to add as new component instead of overwriting existing.",
                    "scene.mutate",
                    true,
                    CommandSchema.Object(
                        ("id", CommandSchema.Integer("GameObject instanceId.")),
                        ("path", CommandSchema.String("Hierarchy path.")),
                        ("name", CommandSchema.String("GameObject name.")),
                        ("component", CommandSchema.String("Component type to paste values onto.")),
                        ("asNew", CommandSchema.Boolean("If true, paste as a new component instead of overwriting. Default false.")))),
                ComponentPaste);
        }

        // args: {"id":12345,"component":"BoxCollider"} or {"name":"Ship","component":"BoxCollider"}
        private static JObject AddComponent(JObject args)
        {
            var (go, err) = CommandRegistry.ResolveGameObject(args);
            if (go == null) return CommandRegistry.Error(err);

            var typeName = args["component"]?.Value<string>();
            if (string.IsNullOrEmpty(typeName))
                return CommandRegistry.Error("Missing 'component' type name");

            var type = TypeHelper.FindType(typeName);
            if (type == null)
                return CommandRegistry.Error($"Type not found: {typeName}");

            if (!typeof(Component).IsAssignableFrom(type) || type.IsAbstract)
                return CommandRegistry.Error($"'{typeName}' is not a concrete Component type");

            go.AddComponent(type);
            EditorUtility.SetDirty(go);
            return CommandRegistry.Ok($"Added {typeName} to {go.name}");
        }

        // args: {"id":12345,"component":"BoxCollider"} or {"name":"Ship","component":"BoxCollider"}
        private static JObject RemoveComponent(JObject args)
        {
            var (go, err) = CommandRegistry.ResolveGameObject(args);
            if (go == null) return CommandRegistry.Error(err);

            var typeName = args["component"]?.Value<string>();
            if (string.IsNullOrEmpty(typeName))
                return CommandRegistry.Error("Missing 'component' type name");

            var type = TypeHelper.FindType(typeName);
            if (type == null)
                return CommandRegistry.Error($"Type not found: {typeName}");

            var comp = go.GetComponent(type);
            if (comp == null)
                return CommandRegistry.Error($"{typeName} not found on {go.name}");

            Undo.DestroyObjectImmediate(comp);
            return CommandRegistry.Ok($"Removed {typeName} from {go.name}");
        }

        // args: {"id":12345,"component":"Transform"} — omit component to list all
        private static JObject GetProperties(JObject args)
        {
            var (go, err) = CommandRegistry.ResolveGameObject(args);
            if (go == null) return CommandRegistry.Error(err);

            var typeName = args["component"]?.Value<string>();
            bool structured = args["structured"]?.Value<bool>() ?? false;
            var result = new JObject();

            if (!string.IsNullOrEmpty(typeName))
            {
                var type = TypeHelper.FindType(typeName);
                if (type == null)
                    return CommandRegistry.Error($"Type not found: {typeName}");

                var comp = go.GetComponent(type);
                if (comp == null)
                    return CommandRegistry.Error($"Component not found: {typeName}");

                result[typeName] = SerializeProperties(comp, structured);
            }
            else
            {
                foreach (var comp in go.GetComponents<Component>())
                {
                    if (comp == null) continue;
                    result[comp.GetType().Name] = SerializeProperties(comp, structured);
                }
            }

            return CommandRegistry.Ok(result);
        }

        // args: {"id":12345,"component":"Transform","property":"m_LocalPosition.x","value":5}
        // value can be: string/number/bool (scalars) or array [x,y,z] / object {x,y,z} (Vector/Color/Rect/Quaternion)
        private static JObject SetProperty(JObject args)
        {
            var (go, err) = CommandRegistry.ResolveGameObject(args);
            if (go == null) return CommandRegistry.Error(err);

            var typeName = args["component"]?.Value<string>();
            var propName = args["property"]?.Value<string>();
            var valueToken = args["value"];

            if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(propName) || valueToken == null)
                return CommandRegistry.Error("Required: 'component', 'property', 'value'");

            var type = TypeHelper.FindType(typeName);
            if (type == null) return CommandRegistry.Error($"Type not found: {typeName}");

            var comp = go.GetComponent(type);
            if (comp == null) return CommandRegistry.Error($"Component not found: {typeName}");

            var so = new SerializedObject(comp);
            var prop = so.FindProperty(propName);
            if (prop == null) return CommandRegistry.Error($"Property not found: {propName}");

            if (!SetPropertyValue(prop, valueToken))
                return CommandRegistry.Error($"Cannot set {prop.propertyType} from {valueToken.Type}: {valueToken}");

            so.ApplyModifiedProperties();
            return CommandRegistry.Ok($"Set {propName} on {go.name}.{typeName}");
        }

        // Helper: resolve component + serialized property by name, shared by array ops.
        private static (SerializedObject so, SerializedProperty prop, JObject error) ResolveArrayProperty(JObject args)
        {
            var (go, err) = CommandRegistry.ResolveGameObject(args);
            if (go == null) return (null, null, CommandRegistry.Error(err));

            var typeName = args["component"]?.Value<string>();
            var propName = args["property"]?.Value<string>();
            if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(propName))
                return (null, null, CommandRegistry.Error("Required: 'component', 'property'"));

            var type = TypeHelper.FindType(typeName);
            if (type == null) return (null, null, CommandRegistry.Error($"Type not found: {typeName}"));

            var comp = go.GetComponent(type);
            if (comp == null) return (null, null, CommandRegistry.Error($"Component not found: {typeName}"));

            var so = new SerializedObject(comp);
            var prop = so.FindProperty(propName);
            if (prop == null) return (null, null, CommandRegistry.Error($"Property not found: {propName}"));
            if (!prop.isArray) return (null, null, CommandRegistry.Error($"Property is not an array: {propName}"));

            return (so, prop, null);
        }

        // args: {"id":..,"component":"Foo","property":"items"} → returns size
        //       {"id":..,"component":"Foo","property":"items","size":5} → resizes and returns new size
        private static JObject ArraySize(JObject args)
        {
            var (so, prop, err) = ResolveArrayProperty(args);
            if (err != null) return err;

            var newSize = args["size"]?.Value<int?>();
            if (newSize.HasValue)
            {
                if (newSize.Value < 0) return CommandRegistry.Error("size must be >= 0");
                prop.arraySize = newSize.Value;
                so.ApplyModifiedProperties();
            }

            return CommandRegistry.Ok(new JObject { ["size"] = prop.arraySize });
        }

        // args: {"id":..,"component":"Foo","property":"items","index":2,"value":<optional>}
        private static JObject ArrayInsert(JObject args)
        {
            var (so, prop, err) = ResolveArrayProperty(args);
            if (err != null) return err;

            int index = args["index"]?.Value<int?>() ?? prop.arraySize;
            if (index < 0 || index > prop.arraySize)
                return CommandRegistry.Error($"index out of range: {index} (size={prop.arraySize})");

            prop.InsertArrayElementAtIndex(index);
            var element = prop.GetArrayElementAtIndex(index);

            var valueToken = args["value"];
            if (valueToken != null && !SetPropertyValue(element, valueToken))
            {
                // Revert the insert on value failure
                prop.DeleteArrayElementAtIndex(index);
                so.ApplyModifiedProperties();
                return CommandRegistry.Error($"Cannot set inserted element from {valueToken.Type}");
            }

            so.ApplyModifiedProperties();
            return CommandRegistry.Ok(new JObject { ["index"] = index, ["size"] = prop.arraySize });
        }

        // args: {"id":..,"component":"Foo","property":"items","index":2}
        private static JObject ArrayDelete(JObject args)
        {
            var (so, prop, err) = ResolveArrayProperty(args);
            if (err != null) return err;

            var indexToken = args["index"];
            if (indexToken == null) return CommandRegistry.Error("Required: 'index'");
            int index = indexToken.Value<int>();
            if (index < 0 || index >= prop.arraySize)
                return CommandRegistry.Error($"index out of range: {index} (size={prop.arraySize})");

            // DeleteArrayElementAtIndex twice for ObjectReference arrays (first call clears, second removes)
            bool wasObjectRef = prop.arrayElementType.StartsWith("PPtr<") &&
                                prop.GetArrayElementAtIndex(index).objectReferenceValue != null;
            prop.DeleteArrayElementAtIndex(index);
            if (wasObjectRef && prop.arraySize > index &&
                prop.GetArrayElementAtIndex(index).objectReferenceValue == null)
            {
                prop.DeleteArrayElementAtIndex(index);
            }
            so.ApplyModifiedProperties();

            return CommandRegistry.Ok(new JObject { ["size"] = prop.arraySize });
        }

        // args: {"id":..,"text":"Hello"} or {"id":..,"text":"Hello","inChildren":true}
        private static JObject SetText(JObject args)
        {
            var (go, err) = CommandRegistry.ResolveGameObject(args);
            if (go == null) return CommandRegistry.Error(err);

            var text = args["text"]?.Value<string>();
            if (text == null) return CommandRegistry.Error("Missing 'text'");

            bool inChildren = args["inChildren"]?.Value<bool>() ?? false;

            // Try TMP first, then uGUI Text. Use short names since TypeHelper.FindType matches by short name.
            string[] typeNames = { "TextMeshProUGUI", "TextMeshPro", "TMP_Text", "Text" };
            foreach (var typeName in typeNames)
            {
                var type = TypeHelper.FindType(typeName);
                if (type == null) continue;

                Component comp = inChildren
                    ? go.GetComponentInChildren(type, true)
                    : go.GetComponent(type);
                if (comp == null) continue;

                var textProp = type.GetProperty("text");
                if (textProp == null) continue;

                Undo.RecordObject(comp, "Set Text");
                textProp.SetValue(comp, text);
                EditorUtility.SetDirty(comp);
                return CommandRegistry.Ok($"Set text on {go.name}.{type.Name}");
            }

            return CommandRegistry.Error("No text component (TMP_Text/TextMeshProUGUI/Text) found on GameObject");
        }

        // args: {"id":..,"component":"Foo","property":"items","fromIndex":1,"toIndex":3}
        private static JObject ArrayMove(JObject args)
        {
            var (so, prop, err) = ResolveArrayProperty(args);
            if (err != null) return err;

            var fromToken = args["fromIndex"];
            var toToken = args["toIndex"];
            if (fromToken == null || toToken == null)
                return CommandRegistry.Error("Required: 'fromIndex', 'toIndex'");

            int from = fromToken.Value<int>();
            int to = toToken.Value<int>();
            if (from < 0 || from >= prop.arraySize || to < 0 || to >= prop.arraySize)
                return CommandRegistry.Error($"index out of range (size={prop.arraySize})");

            prop.MoveArrayElement(from, to);
            so.ApplyModifiedProperties();
            return CommandRegistry.Ok(new JObject { ["from"] = from, ["to"] = to, ["size"] = prop.arraySize });
        }

        // args: {"id":..,"component":"Foo","property":"items"} → {"size":N,"elements":[...]}
        private static JObject GetArray(JObject args)
        {
            var (so, prop, err) = ResolveArrayProperty(args);
            if (err != null) return err;

            var elements = new JArray();
            for (int i = 0; i < prop.arraySize; i++)
            {
                var element = prop.GetArrayElementAtIndex(i);
                elements.Add(SerializeElementStructured(element));
            }

            return CommandRegistry.Ok(new JObject
            {
                ["size"] = prop.arraySize,
                ["elements"] = elements
            });
        }

        // Serialize a single SerializedProperty as structured JSON, including nested objects.
        private static JToken SerializeElementStructured(SerializedProperty prop)
        {
            // For primitive-like types, use existing structured serializer
            var simple = GetPropertyValueStructured(prop);
            if (prop.propertyType != SerializedPropertyType.Generic)
                return simple;

            // Generic (struct/class): walk children
            var obj = new JObject();
            var end = prop.GetEndProperty();
            var iter = prop.Copy();
            if (iter.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(iter, end)) break;
                    obj[iter.name] = iter.propertyType == SerializedPropertyType.Generic
                        ? SerializeElementStructured(iter)
                        : GetPropertyValueStructured(iter);
                } while (iter.NextVisible(false));
            }
            return obj;
        }

        // args: {"id":..,"component":"Foo","method":"Bar","params":[...]}
        private static JObject CallMethod(JObject args)
        {
            var (go, err) = CommandRegistry.ResolveGameObject(args);
            if (go == null) return CommandRegistry.Error(err);

            var typeName = args["component"]?.Value<string>();
            var methodName = args["method"]?.Value<string>();
            if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(methodName))
                return CommandRegistry.Error("Required: 'component', 'method'");

            var type = TypeHelper.FindType(typeName);
            if (type == null) return CommandRegistry.Error($"Type not found: {typeName}");

            var comp = go.GetComponent(type);
            if (comp == null) return CommandRegistry.Error($"Component not found: {typeName}");

            bool nonPublic = args["nonPublic"]?.Value<bool>() ?? false;
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;
            if (nonPublic) flags |= System.Reflection.BindingFlags.NonPublic;

            var paramArr = args["params"] as JArray;
            int paramCount = paramArr?.Count ?? 0;

            // Find matching method by name + parameter count
            System.Reflection.MethodInfo method = null;
            foreach (var m in type.GetMethods(flags))
            {
                if (m.Name == methodName && m.GetParameters().Length == paramCount)
                {
                    method = m;
                    break;
                }
            }
            if (method == null)
                return CommandRegistry.Error($"Method not found: {typeName}.{methodName} (params: {paramCount})");

            // Convert JSON params to method parameter types
            var methodParams = method.GetParameters();
            var callArgs = new object[paramCount];
            for (int i = 0; i < paramCount; i++)
            {
                try
                {
                    callArgs[i] = paramArr[i].ToObject(methodParams[i].ParameterType);
                }
                catch (Exception ex)
                {
                    return CommandRegistry.Error($"Cannot convert param {i} to {methodParams[i].ParameterType.Name}: {ex.Message}");
                }
            }

            try
            {
                var result = method.Invoke(comp, callArgs);
                EditorUtility.SetDirty(comp);
                return CommandRegistry.Ok(new JObject
                {
                    ["returnType"] = method.ReturnType.Name,
                    ["result"] = result == null ? null : JToken.FromObject(result)
                });
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException ?? ex;
                return CommandRegistry.Error($"Invocation failed: {inner.GetType().Name}: {inner.Message}");
            }
        }

        // args: {"id":..,"component":"Foo","field":"Bar"} — tries field first, then property
        private static JObject GetField(JObject args)
        {
            var (comp, member, err) = ResolveMember(args);
            if (err != null) return err;

            try
            {
                object value;
                if (member is System.Reflection.FieldInfo f)
                    value = f.GetValue(comp);
                else if (member is System.Reflection.PropertyInfo p)
                    value = p.GetValue(comp);
                else
                    return CommandRegistry.Error("Member is neither field nor property");

                return CommandRegistry.Ok(new JObject
                {
                    ["value"] = value == null ? null : JToken.FromObject(value),
                    ["type"] = (member is System.Reflection.FieldInfo fi ? fi.FieldType : ((System.Reflection.PropertyInfo)member).PropertyType).Name
                });
            }
            catch (Exception ex)
            {
                return CommandRegistry.Error($"Read failed: {ex.Message}");
            }
        }

        // args: {"id":..,"component":"Foo","field":"Bar","value":<json>}
        private static JObject SetField(JObject args)
        {
            var (comp, member, err) = ResolveMember(args);
            if (err != null) return err;

            var valueToken = args["value"];
            if (valueToken == null) return CommandRegistry.Error("Missing 'value'");

            try
            {
                Type targetType;
                if (member is System.Reflection.FieldInfo f)
                {
                    targetType = f.FieldType;
                    f.SetValue(comp, valueToken.ToObject(targetType));
                }
                else if (member is System.Reflection.PropertyInfo p)
                {
                    if (!p.CanWrite) return CommandRegistry.Error($"Property is read-only: {p.Name}");
                    targetType = p.PropertyType;
                    p.SetValue(comp, valueToken.ToObject(targetType));
                }
                else
                {
                    return CommandRegistry.Error("Member is neither field nor property");
                }

                EditorUtility.SetDirty(comp);
                return CommandRegistry.Ok($"Set {member.Name} on {comp.GetType().Name}");
            }
            catch (Exception ex)
            {
                return CommandRegistry.Error($"Write failed: {ex.Message}");
            }
        }

        // args: {"id":<buttonId>} — invokes button.onClick.Invoke() via reflection
        private static JObject ButtonClick(JObject args)
        {
            var (go, err) = CommandRegistry.ResolveGameObject(args);
            if (go == null) return CommandRegistry.Error(err);

            var buttonType = TypeHelper.FindType("Button");
            if (buttonType == null) return CommandRegistry.Error("UI.Button type not found");

            var button = go.GetComponent(buttonType);
            if (button == null) return CommandRegistry.Error($"No Button on {go.name}");

            // Check interactable
            var interactableProp = buttonType.GetProperty("interactable");
            if (interactableProp != null && !(bool)interactableProp.GetValue(button))
                return CommandRegistry.Error("Button is not interactable");

            // Get onClick and invoke
            var onClickProp = buttonType.GetProperty("onClick");
            if (onClickProp == null) return CommandRegistry.Error("Button has no onClick property");
            var onClick = onClickProp.GetValue(button);
            if (onClick == null) return CommandRegistry.Error("onClick is null");

            var invokeMethod = onClick.GetType().GetMethod("Invoke", Type.EmptyTypes);
            if (invokeMethod == null) return CommandRegistry.Error("onClick.Invoke() not found");

            try
            {
                invokeMethod.Invoke(onClick, null);
                return CommandRegistry.Ok($"Clicked {go.name}");
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException ?? ex;
                return CommandRegistry.Error($"onClick handler threw: {inner.Message}");
            }
        }

        // args: {"id":..,"component":"Rigidbody"}
        private static JObject ComponentCopy(JObject args)
        {
            var (go, err) = CommandRegistry.ResolveGameObject(args);
            if (go == null) return CommandRegistry.Error(err);

            var typeName = args["component"]?.Value<string>();
            if (string.IsNullOrEmpty(typeName)) return CommandRegistry.Error("Missing 'component'");
            var type = TypeHelper.FindType(typeName);
            if (type == null) return CommandRegistry.Error($"Type not found: {typeName}");

            var comp = go.GetComponent(type);
            if (comp == null) return CommandRegistry.Error($"Component not found: {typeName}");

            bool ok = UnityEditorInternal.ComponentUtility.CopyComponent(comp);
            return ok
                ? CommandRegistry.Ok($"Copied {typeName} from {go.name}")
                : CommandRegistry.Error("Copy failed");
        }

        // args: {"id":..,"component":"Rigidbody","asNew":false}
        private static JObject ComponentPaste(JObject args)
        {
            var (go, err) = CommandRegistry.ResolveGameObject(args);
            if (go == null) return CommandRegistry.Error(err);

            bool asNew = args["asNew"]?.Value<bool>() ?? false;

            if (asNew)
            {
                bool added = UnityEditorInternal.ComponentUtility.PasteComponentAsNew(go);
                return added
                    ? CommandRegistry.Ok($"Pasted as new component on {go.name}")
                    : CommandRegistry.Error("Paste as new failed");
            }

            // Paste values onto existing component
            var typeName = args["component"]?.Value<string>();
            if (string.IsNullOrEmpty(typeName)) return CommandRegistry.Error("Missing 'component' (or pass asNew:true)");
            var type = TypeHelper.FindType(typeName);
            if (type == null) return CommandRegistry.Error($"Type not found: {typeName}");

            var comp = go.GetComponent(type);
            if (comp == null) return CommandRegistry.Error($"Component not found: {typeName}");

            bool ok = UnityEditorInternal.ComponentUtility.PasteComponentValues(comp);
            return ok
                ? CommandRegistry.Ok($"Pasted values onto {go.name}.{typeName}")
                : CommandRegistry.Error("Paste values failed");
        }

        // Resolves a component + member (field or property) by name.
        private static (Component comp, System.Reflection.MemberInfo member, JObject error) ResolveMember(JObject args)
        {
            var (go, err) = CommandRegistry.ResolveGameObject(args);
            if (go == null) return (null, null, CommandRegistry.Error(err));

            var typeName = args["component"]?.Value<string>();
            var fieldName = args["field"]?.Value<string>();
            if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(fieldName))
                return (null, null, CommandRegistry.Error("Required: 'component', 'field'"));

            var type = TypeHelper.FindType(typeName);
            if (type == null) return (null, null, CommandRegistry.Error($"Type not found: {typeName}"));

            var comp = go.GetComponent(type);
            if (comp == null) return (null, null, CommandRegistry.Error($"Component not found: {typeName}"));

            bool nonPublic = args["nonPublic"]?.Value<bool>() ?? true;
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;
            if (nonPublic) flags |= System.Reflection.BindingFlags.NonPublic;

            // Walk the type hierarchy to find field or property (including inherited private)
            var t = type;
            while (t != null)
            {
                var field = t.GetField(fieldName, flags);
                if (field != null) return (comp, field, null);
                var prop = t.GetProperty(fieldName, flags);
                if (prop != null) return (comp, prop, null);
                t = t.BaseType;
            }

            return (null, null, CommandRegistry.Error($"Field or property not found: {typeName}.{fieldName}"));
        }

        private static JObject SerializeProperties(Component comp, bool structured = false)
        {
            var props = new JObject();
            var so = new SerializedObject(comp);
            var iter = so.GetIterator();
            if (iter.NextVisible(true))
            {
                do
                {
                    props[iter.name] = structured
                        ? GetPropertyValueStructured(iter)
                        : (JToken)$"{GetPropertyValue(iter)} ({iter.propertyType})";
                } while (iter.NextVisible(false));
            }
            return props;
        }

        // Legacy string formatter — preserved for backward compat.
        private static string GetPropertyValue(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue.ToString();
                case SerializedPropertyType.Float: return prop.floatValue.ToString("F4");
                case SerializedPropertyType.Boolean: return prop.boolValue.ToString();
                case SerializedPropertyType.String: return prop.stringValue;
                case SerializedPropertyType.Vector2: return prop.vector2Value.ToString();
                case SerializedPropertyType.Vector3: return prop.vector3Value.ToString();
                case SerializedPropertyType.Enum: return prop.enumNames[prop.enumValueIndex];
                case SerializedPropertyType.ObjectReference:
                    return prop.objectReferenceValue != null ? prop.objectReferenceValue.name : "null";
                default: return $"({prop.propertyType})";
            }
        }

        // Native JSON serializer — returns numbers/arrays/objects instead of strings.
        private static JToken GetPropertyValueStructured(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue;
                case SerializedPropertyType.Float: return prop.floatValue;
                case SerializedPropertyType.Boolean: return prop.boolValue;
                case SerializedPropertyType.String: return prop.stringValue;
                case SerializedPropertyType.Enum:
                    return new JObject
                    {
                        ["value"] = prop.enumValueIndex,
                        ["name"] = prop.enumValueIndex >= 0 && prop.enumValueIndex < prop.enumNames.Length
                            ? prop.enumNames[prop.enumValueIndex] : null
                    };
                case SerializedPropertyType.Vector2:
                    return new JArray(prop.vector2Value.x, prop.vector2Value.y);
                case SerializedPropertyType.Vector3:
                    return new JArray(prop.vector3Value.x, prop.vector3Value.y, prop.vector3Value.z);
                case SerializedPropertyType.Vector4:
                    return new JArray(prop.vector4Value.x, prop.vector4Value.y, prop.vector4Value.z, prop.vector4Value.w);
                case SerializedPropertyType.Vector2Int:
                    return new JArray(prop.vector2IntValue.x, prop.vector2IntValue.y);
                case SerializedPropertyType.Vector3Int:
                    return new JArray(prop.vector3IntValue.x, prop.vector3IntValue.y, prop.vector3IntValue.z);
                case SerializedPropertyType.Quaternion:
                    var q = prop.quaternionValue;
                    return new JArray(q.x, q.y, q.z, q.w);
                case SerializedPropertyType.Color:
                    var c = prop.colorValue;
                    return new JArray(c.r, c.g, c.b, c.a);
                case SerializedPropertyType.Rect:
                    var r = prop.rectValue;
                    return new JArray(r.x, r.y, r.width, r.height);
                case SerializedPropertyType.Bounds:
                    var b = prop.boundsValue;
                    return new JObject
                    {
                        ["center"] = new JArray(b.center.x, b.center.y, b.center.z),
                        ["size"] = new JArray(b.size.x, b.size.y, b.size.z)
                    };
                case SerializedPropertyType.ObjectReference:
                    if (prop.objectReferenceValue == null) return null;
                    return new JObject
                    {
                        ["name"] = prop.objectReferenceValue.name,
                        ["instanceId"] = prop.objectReferenceValue.GetInstanceID(),
                        ["type"] = prop.objectReferenceValue.GetType().Name
                    };
                case SerializedPropertyType.LayerMask:
                    return prop.intValue;
                case SerializedPropertyType.ArraySize:
                    return prop.intValue;
                default: return $"<{prop.propertyType}>";
            }
        }

        // Accepts JToken: primitive for scalars, JArray/JObject for Vector/Color/Rect/Quaternion.
        private static bool SetPropertyValue(SerializedProperty prop, JToken token)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.ArraySize:
                    prop.intValue = token.Value<int>(); return true;
                case SerializedPropertyType.Float:
                    prop.floatValue = token.Value<float>(); return true;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = token.Value<bool>(); return true;
                case SerializedPropertyType.String:
                    prop.stringValue = token.Value<string>(); return true;
                case SerializedPropertyType.Enum:
                {
                    if (token.Type == JTokenType.Integer)
                    {
                        var enumIndex = token.Value<int>();
                        if (enumIndex < 0 || enumIndex >= prop.enumNames.Length) return false;
                        prop.enumValueIndex = enumIndex;
                        return true;
                    }
                    var enumName = token.Value<string>();
                    var idx = Array.IndexOf(prop.enumNames, enumName);
                    if (idx >= 0) { prop.enumValueIndex = idx; return true; }
                    return false;
                }
                case SerializedPropertyType.ObjectReference:
                {
                    // Accept integer instanceId directly, or string that parses as int.
                    int instanceId;
                    if (token.Type == JTokenType.Integer) instanceId = token.Value<int>();
                    else if (!int.TryParse(token.Value<string>(), out instanceId)) return false;
                    var obj = EditorUtility.InstanceIDToObject(instanceId);
                    if (obj == null) return false;
                    prop.objectReferenceValue = obj;
                    return true;
                }
                case SerializedPropertyType.Vector2:
                {
                    if (!TryReadVector(token, 2, out var v)) return false;
                    prop.vector2Value = new Vector2(v[0], v[1]);
                    return true;
                }
                case SerializedPropertyType.Vector3:
                {
                    if (!TryReadVector(token, 3, out var v)) return false;
                    prop.vector3Value = new Vector3(v[0], v[1], v[2]);
                    return true;
                }
                case SerializedPropertyType.Vector4:
                {
                    if (!TryReadVector(token, 4, out var v)) return false;
                    prop.vector4Value = new Vector4(v[0], v[1], v[2], v[3]);
                    return true;
                }
                case SerializedPropertyType.Vector2Int:
                {
                    if (!TryReadVector(token, 2, out var v)) return false;
                    prop.vector2IntValue = new Vector2Int((int)v[0], (int)v[1]);
                    return true;
                }
                case SerializedPropertyType.Vector3Int:
                {
                    if (!TryReadVector(token, 3, out var v)) return false;
                    prop.vector3IntValue = new Vector3Int((int)v[0], (int)v[1], (int)v[2]);
                    return true;
                }
                case SerializedPropertyType.Quaternion:
                {
                    if (!TryReadVector(token, 4, out var v)) return false;
                    prop.quaternionValue = new Quaternion(v[0], v[1], v[2], v[3]);
                    return true;
                }
                case SerializedPropertyType.Color:
                {
                    if (!TryReadColor(token, out var c)) return false;
                    prop.colorValue = c;
                    return true;
                }
                case SerializedPropertyType.Rect:
                {
                    if (!TryReadVector(token, 4, out var v)) return false;
                    prop.rectValue = new Rect(v[0], v[1], v[2], v[3]);
                    return true;
                }
                case SerializedPropertyType.Bounds:
                {
                    // Accept object {center:[x,y,z], size:[x,y,z]} only
                    if (!(token is JObject bo)) return false;
                    if (!(bo["center"] is JToken centerTok) || !(bo["size"] is JToken sizeTok)) return false;
                    if (!TryReadVector(centerTok, 3, out var center)) return false;
                    if (!TryReadVector(sizeTok, 3, out var size)) return false;
                    prop.boundsValue = new Bounds(
                        new Vector3(center[0], center[1], center[2]),
                        new Vector3(size[0], size[1], size[2]));
                    return true;
                }
            }
            return false;
        }

        // Reads a vector from JArray [x,y,z] or JObject {x:..,y:..,z:..,w:..}
        private static bool TryReadVector(JToken token, int count, out float[] result)
        {
            result = new float[count];
            if (token is JArray arr)
            {
                if (arr.Count < count) return false;
                for (int i = 0; i < count; i++)
                    result[i] = arr[i].Value<float>();
                return true;
            }
            if (token is JObject obj)
            {
                string[] keys = { "x", "y", "z", "w" };
                for (int i = 0; i < count; i++)
                {
                    var v = obj[keys[i]];
                    if (v == null) return false;
                    result[i] = v.Value<float>();
                }
                return true;
            }
            return false;
        }

        // Reads a color from JArray [r,g,b] / [r,g,b,a] or JObject {r,g,b,a}
        private static bool TryReadColor(JToken token, out Color color)
        {
            color = Color.white;
            if (token is JArray arr && (arr.Count == 3 || arr.Count == 4))
            {
                color = new Color(
                    arr[0].Value<float>(),
                    arr[1].Value<float>(),
                    arr[2].Value<float>(),
                    arr.Count == 4 ? arr[3].Value<float>() : 1f);
                return true;
            }
            if (token is JObject obj)
            {
                var r = obj["r"]; var g = obj["g"]; var b = obj["b"]; var a = obj["a"];
                if (r == null || g == null || b == null) return false;
                color = new Color(
                    r.Value<float>(), g.Value<float>(), b.Value<float>(),
                    a != null ? a.Value<float>() : 1f);
                return true;
            }
            return false;
        }
    }
}
