using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
#if CAPYBARA_SERVER
using System.Text.Json;
#else
using Newtonsoft.Json;
using UnityEngine;
#endif

namespace CapybaraRebuild
{
    public sealed class AdventureCatalog
    {
        public GameData Data { get; }
        public string Version { get; }

        private AdventureCatalog(byte[] chapters, byte[] stops, byte[] eventModels)
        {
            string chapterJson = Decode(chapters);
            string stopsJson = Decode(stops);
            string eventModelsJson = Decode(eventModels);
            Data = Read<GameData>(chapterJson);
            if (Data?.chapters == null || Data.chapters.Length == 0)
                throw new InvalidDataException("The Capybara chapter catalog is empty.");
            Data.offlineJourneyStops = Read<OfflineJourneyStops>(stopsJson);
            Data.journeyEventModels = Read<JourneyEventModels>(eventModelsJson);

            using var stream = new MemoryStream();
            using var hash = SHA256.Create();
            foreach (string file in new[] { chapterJson, stopsJson, eventModelsJson })
            {
                byte[] digest = hash.ComputeHash(Encoding.UTF8.GetBytes(file));
                stream.Write(digest, 0, digest.Length);
            }
            stream.Position = 0;
            Version = Convert.ToBase64String(hash.ComputeHash(stream));
        }

        public static AdventureCatalog Load() =>
            new AdventureCatalog(LoadFile("GameData"), LoadFile("OfflineJourneyStops"), LoadFile("JourneyEventModels"));

        private static string Decode(byte[] bytes) => Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF').Replace("\r\n", "\n");

        private static byte[] LoadFile(string name)
        {
#if CAPYBARA_SERVER
            using Stream resource = typeof(AdventureCatalog).Assembly.GetManifestResourceStream(name + ".json")
                ?? throw new FileNotFoundException(name + ".json");
            using var buffer = new MemoryStream();
            resource.CopyTo(buffer);
            return buffer.ToArray();
#else
            var asset = Resources.Load<TextAsset>(name);
            if (asset == null) throw new FileNotFoundException(name + ".json");
            return asset.bytes;
#endif
        }

#if CAPYBARA_SERVER
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            IncludeFields = true,
            IgnoreReadOnlyProperties = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private static readonly Lazy<AdventureCatalog> ServerCatalog = new Lazy<AdventureCatalog>(Load);
        public static AdventureCatalog Server => ServerCatalog.Value;

        public static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, JsonOptions);
        public static string Write<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
#else
        public static T Read<T>(string json) => JsonConvert.DeserializeObject<T>(json);
#endif
    }
}
