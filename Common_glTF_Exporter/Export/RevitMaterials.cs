namespace Common_glTF_Exporter.Export
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Collections.Generic;
    using Autodesk.Revit.DB;
    using Common_glTF_Exporter.Core;
    using Revit_glTF_Exporter;
    using Newtonsoft.Json;

    internal record MaterialConfig(
        string Name,
        [JsonProperty("base_color")]
        List<double> Color,
        double Alpha,
        double Metallic,
        double Roughness)
    {
        const string DefaultPath = "C:\\Users\\user\\Documents\\CoreMaterials.json";

        public static Dictionary<string, MaterialConfig> ReadFile(string? path = null)
        {
            var _path = path ?? DefaultPath;
            if (!File.Exists(_path)) throw new InvalidOperationException($"Missing config {_path}");
            var materials = JsonConvert.DeserializeObject<List<MaterialConfig>>(File.ReadAllText(_path));
            if (materials is null) throw new InvalidOperationException("Failed to deserialize material config");
            return materials.ToDictionary(m => m.Name);
        }
    }

    public static class RevitMaterials
    {
        const string BLEND = "BLEND";
        const string OPAQUE = "OPAQUE";
        const int ONEINTVALUE = 1;

        /// <summary>
        /// Container for material names (Local cache to avoid Revit API I/O)
        /// </summary>
        static Dictionary<ElementId, MaterialCacheDTO> MaterialNameContainer = new Dictionary<ElementId, MaterialCacheDTO>();

        static readonly Lazy<Dictionary<string, MaterialConfig>> MatConfig = new(() => MaterialConfig.ReadFile());

        /// <summary>
        /// Export Revit materials.
        /// </summary>
        /// <param name="node">node.</param>
        /// <param name="doc">Revit document.</param>
        /// <param name="materials">Materials.</param>
        public static void Export(MaterialNode node, Document doc, ref IndexedDictionary<GLTFMaterial> materials)
        {
            ElementId id = node.MaterialId;
            GLTFMaterial gl_mat = new GLTFMaterial();
            float opacity = ONEINTVALUE - (float)node.Transparency;

            // Validate if the material is valid because for some reason there are
            // materials with invalid Ids
            if (id != ElementId.InvalidElementId)
            {
                string uniqueId;
                if (!MaterialNameContainer.TryGetValue(node.MaterialId, out var materialElement))
                {
                    // construct a material from the node
                    var m = doc.GetElement(node.MaterialId);
                    gl_mat.name = m.Name;
                    uniqueId = m.UniqueId;
                    MaterialNameContainer.Add(node.MaterialId, new MaterialCacheDTO(m.Name, m.UniqueId));
                }
                else
                {
                    var elementData = MaterialNameContainer[node.MaterialId];
                    gl_mat.name = elementData.MaterialName;
                    uniqueId = elementData.UniqueId;
                }

                MatConfig.Value.TryGetValue(gl_mat.name, out var config);

                GLTFPBR pbr = new GLTFPBR();
                SetMaterialsProperties(node, opacity, ref pbr, ref gl_mat, config);

                materials.AddOrUpdateCurrentMaterial(uniqueId, gl_mat, false);
            }
        }

        public static bool AlmostEqual(double lhs, double rhs, double delta = double.Epsilon) =>
            Math.Abs(lhs - rhs) < delta;

        private static void SetMaterialsProperties(MaterialNode node, float opacity, ref GLTFPBR pbr, ref GLTFMaterial gl_mat, MaterialConfig? config)
        {
            if (config is null)
            {
                pbr.baseColorFactor = new List<float>(4) { node.Color.Red / 255f, node.Color.Green / 255f, node.Color.Blue / 255f, opacity };
                pbr.metallicFactor = 0f;
                pbr.roughnessFactor = opacity != 1 ? 0.5f : 1f;
                gl_mat.pbrMetallicRoughness = pbr;

                // TODO: Implement MASK alphamode for elements like leaves or wire fences
                gl_mat.alphaMode = opacity != 1 ? BLEND : OPAQUE;
                gl_mat.alphaCutoff = null;
            } else {
                pbr.baseColorFactor = [(float)config.Color[0], (float)config.Color[1], (float)config.Color[2], (float)config.Alpha];
                pbr.metallicFactor = (float)config.Metallic;
                pbr.roughnessFactor = (float)config.Roughness;
                gl_mat.pbrMetallicRoughness = pbr;

                gl_mat.alphaMode = AlmostEqual(config.Alpha, 1) ? OPAQUE : BLEND;
                gl_mat.alphaCutoff = null;
            }
        }
    }

    public class MaterialCacheDTO
    {
        public MaterialCacheDTO(string materialName, string uniqueId)
        {
            MaterialName = materialName;
            UniqueId = uniqueId;
        }

        public string MaterialName { get; set; }

        public string UniqueId { get; set; }
    }
}
