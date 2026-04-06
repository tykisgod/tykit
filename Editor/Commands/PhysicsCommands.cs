// Commands/PhysicsCommands.cs
using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

namespace Tykit
{
    [InitializeOnLoad]
    public static class PhysicsCommands
    {
        static PhysicsCommands()
        {
            RegisterCommands();
        }

        public static void RegisterCommands()
        {
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "raycast",
                    "Cast a ray and return the first hit (if any).",
                    "scene.query",
                    false,
                    CommandSchema.Object(
                        ("origin", CommandSchema.Array(CommandSchema.Number(), "[x,y,z] ray origin.")),
                        ("direction", CommandSchema.Array(CommandSchema.Number(), "[x,y,z] ray direction (will be normalized).")),
                        ("distance", CommandSchema.Number("Max distance. Default 1000.")),
                        ("layerMask", CommandSchema.Integer("LayerMask bitfield. Default -1 (all layers).")))),
                Raycast);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "raycast-all",
                    "Cast a ray and return all hits along the path.",
                    "scene.query",
                    false,
                    CommandSchema.Object(
                        ("origin", CommandSchema.Array(CommandSchema.Number(), "[x,y,z] ray origin.")),
                        ("direction", CommandSchema.Array(CommandSchema.Number(), "[x,y,z] ray direction.")),
                        ("distance", CommandSchema.Number("Max distance. Default 1000.")),
                        ("layerMask", CommandSchema.Integer("LayerMask bitfield. Default -1.")))),
                RaycastAll);
            CommandRegistry.Register(
                CommandRegistry.Describe(
                    "overlap-sphere",
                    "Return all colliders intersecting a sphere at the given position.",
                    "scene.query",
                    false,
                    CommandSchema.Object(
                        ("center", CommandSchema.Array(CommandSchema.Number(), "[x,y,z] sphere center.")),
                        ("radius", CommandSchema.Number("Sphere radius.")),
                        ("layerMask", CommandSchema.Integer("LayerMask bitfield. Default -1.")))),
                OverlapSphere);
        }

        private static bool ReadVector3(JToken token, out Vector3 vec)
        {
            vec = Vector3.zero;
            if (!(token is JArray arr) || arr.Count < 3) return false;
            vec = new Vector3(arr[0].Value<float>(), arr[1].Value<float>(), arr[2].Value<float>());
            return true;
        }

        private static JObject HitToJson(RaycastHit hit)
        {
            return new JObject
            {
                ["point"] = new JArray(hit.point.x, hit.point.y, hit.point.z),
                ["normal"] = new JArray(hit.normal.x, hit.normal.y, hit.normal.z),
                ["distance"] = hit.distance,
                ["colliderName"] = hit.collider != null ? hit.collider.name : null,
                ["colliderId"] = hit.collider != null ? hit.collider.GetInstanceID() : 0,
                ["gameObjectId"] = hit.collider != null ? hit.collider.gameObject.GetInstanceID() : 0,
                ["gameObjectName"] = hit.collider != null ? hit.collider.gameObject.name : null
            };
        }

        private static JObject Raycast(JObject args)
        {
            if (!ReadVector3(args["origin"], out var origin))
                return CommandRegistry.Error("Missing or invalid 'origin' [x,y,z]");
            if (!ReadVector3(args["direction"], out var direction))
                return CommandRegistry.Error("Missing or invalid 'direction' [x,y,z]");

            float distance = args["distance"]?.Value<float?>() ?? 1000f;
            int layerMask = args["layerMask"]?.Value<int?>() ?? -1;

            if (Physics.Raycast(origin, direction.normalized, out var hit, distance, layerMask))
                return CommandRegistry.Ok(new JObject { ["hit"] = true, ["result"] = HitToJson(hit) });

            return CommandRegistry.Ok(new JObject { ["hit"] = false });
        }

        private static JObject RaycastAll(JObject args)
        {
            if (!ReadVector3(args["origin"], out var origin))
                return CommandRegistry.Error("Missing or invalid 'origin'");
            if (!ReadVector3(args["direction"], out var direction))
                return CommandRegistry.Error("Missing or invalid 'direction'");

            float distance = args["distance"]?.Value<float?>() ?? 1000f;
            int layerMask = args["layerMask"]?.Value<int?>() ?? -1;

            var hits = Physics.RaycastAll(origin, direction.normalized, distance, layerMask);
            var arr = new JArray();
            foreach (var h in hits) arr.Add(HitToJson(h));
            return CommandRegistry.Ok(new JObject { ["count"] = hits.Length, ["hits"] = arr });
        }

        private static JObject OverlapSphere(JObject args)
        {
            if (!ReadVector3(args["center"], out var center))
                return CommandRegistry.Error("Missing or invalid 'center'");

            float radius = args["radius"]?.Value<float?>() ?? 1f;
            int layerMask = args["layerMask"]?.Value<int?>() ?? -1;

            var colliders = Physics.OverlapSphere(center, radius, layerMask);
            var arr = new JArray();
            foreach (var c in colliders)
            {
                if (c == null) continue;
                arr.Add(new JObject
                {
                    ["colliderName"] = c.name,
                    ["colliderId"] = c.GetInstanceID(),
                    ["gameObjectId"] = c.gameObject.GetInstanceID(),
                    ["gameObjectName"] = c.gameObject.name,
                    ["position"] = new JArray(c.transform.position.x, c.transform.position.y, c.transform.position.z)
                });
            }
            return CommandRegistry.Ok(new JObject { ["count"] = colliders.Length, ["colliders"] = arr });
        }
    }
}
