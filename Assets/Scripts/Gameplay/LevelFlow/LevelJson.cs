using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Sokoban.Domain;
using UnityEngine;

namespace Sokoban
{
    public static class LevelJson
    {
        private sealed class FieldsOnlyResolver : DefaultContractResolver
        {
            private readonly bool legacy;
            public FieldsOnlyResolver(bool legacy = false) { this.legacy = legacy; }
            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization serialization)
            {
                var hierarchy = new Stack<Type>();
                for (var current = type; current != null && current != typeof(object); current = current.BaseType) hierarchy.Push(current);
                var properties = new List<JsonProperty>();
                while (hierarchy.Count > 0)
                    foreach (var field in hierarchy.Pop().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).OrderBy(f => f.MetadataToken))
                        if (!legacy || field.DeclaringType != typeof(CrateDefinition) || field.Name != nameof(CrateDefinition.kind))
                            properties.Add(CreateProperty(field, MemberSerialization.Fields));
                return properties;
            }
        }
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings { ContractResolver = new FieldsOnlyResolver() };
        private static readonly JsonSerializerSettings LegacySettings = new JsonSerializerSettings { ContractResolver = new FieldsOnlyResolver(true) };
        private static JsonSerializerSettings WireSettings(LevelDefinition level)
        {
            // Retain v1 bytes/hashes for existing energy-only levels and their proofs.
            // Never silently discard a cargo type while serializing a v1 definition.
            if (level.schemaVersion == 1 && level.crates != null && level.crates.Any(c => c != null && !c.IsEnergy))
                throw new FormatException("普通箱需要 schemaVersion=2，不能保存为旧版能源箱数据。");
            return level.schemaVersion == 1 ? LegacySettings : Settings;
        }
        public static string Write(LevelDefinition level) => JsonConvert.SerializeObject(level, Formatting.Indented, WireSettings(level)) + "\n";
        public static LevelDefinition Read(string json)
        {
            var token = JToken.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            Require(token.Type == JTokenType.Object, "$");
            var version = token["schemaVersion"];
            CheckShape(typeof(int), version, "$.schemaVersion", false);
            int schema = (int)version;
            if (schema != 1 && schema != 2) throw new FormatException("仅支持 schemaVersion=1 或 2。");
            if (schema == 1 && token["crates"] is JArray crates)
                foreach (var item in crates.OfType<JObject>())
                {
                    if (item.Property("kind") != null) throw new FormatException("schemaVersion=1 的箱子不能包含 kind；请使用版本 2。");
                    item.Add("kind", CrateDefinition.Energy);
                }
            CheckShape(typeof(LevelDefinition), token, "$", false);
            return token.ToObject<LevelDefinition>(JsonSerializer.Create(Settings));
        }

        // JsonUtility silently defaults missing fields. Validate the wire format first,
        // including false/0 fields, without coupling the rule assembly to a JSON library.
        private static void CheckShape(Type type, JToken token, string path, bool nullable)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                if (nullable) return;
                throw new FormatException(path + " 为必需字段，不能缺失或为 null。");
            }
            if (type == typeof(string)) { Require(token.Type == JTokenType.String, path); return; }
            if (type == typeof(bool)) { Require(token.Type == JTokenType.Boolean, path); return; }
            if (type == typeof(int)) { Require(token.Type == JTokenType.Integer && long.TryParse(token.ToString(), out var n) && n >= int.MinValue && n <= int.MaxValue, path); return; }
            if (type == typeof(float)) { Require(token.Type == JTokenType.Float || token.Type == JTokenType.Integer, path); return; }
            if (type.IsArray)
            {
                Require(token.Type == JTokenType.Array, path);
                int index = 0;
                foreach (var item in token.Children()) CheckShape(type.GetElementType(), item, path + "[" + index++ + "]", false);
                return;
            }
            Require(token.Type == JTokenType.Object, path);
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (((JObject)token).Property(field.Name) == null) throw new FormatException(path + "." + field.Name + " 缺失。");
                CheckShape(field.FieldType, token[field.Name], path + "." + field.Name, field.FieldType == typeof(PlayerSpawn));
            }
            foreach (var property in ((JObject)token).Properties())
                if (!fields.Any(f => f.Name == property.Name)) throw new FormatException(path + "." + property.Name + " 是未知字段。");
        }
        private static void Require(bool condition, string path)
        { if (!condition) throw new FormatException(path + " 字段类型无效。"); }

        public static string Hash(LevelDefinition level)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(level, Formatting.None, WireSettings(level))))).Replace("-", "").ToLowerInvariant();
        }

        public static void AtomicWrite(string path, string contents)
        {
            string absolute = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute));
            string temporary = absolute + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, contents, new UTF8Encoding(false));
                // Unity's asset watcher and Windows indexing can briefly hold the target.
                // Retry the atomic operation; never fall back to deleting the existing file.
                for (int attempt = 0; ; attempt++)
                {
                    try
                    {
                        if (File.Exists(absolute)) File.Replace(temporary, absolute, null);
                        else File.Move(temporary, absolute);
                        break;
                    }
                    catch (IOException) when (attempt < 3) { Thread.Sleep(20 * (attempt + 1)); }
                }
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
