using System;
using System.Collections.Generic;
using System.IO;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using Common_glTF_Exporter.Core;
using Common_glTF_Exporter.Windows.MainWindow;
using Revit_glTF_Exporter;
using Common_glTF_Exporter.Materials;
using Common_glTF_Exporter.Model;
using System.IO.Ports;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using Material = Autodesk.Revit.DB.Material;
using Newtonsoft.Json;

namespace Common_glTF_Exporter.Export
{
    public record MaterialConfig(
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
        const int ONEINTVALUE = 1;

        /// <summary>
        /// Container for material names (Local cache to avoid Revit API I/O)
        /// </summary>
        static Dictionary<ElementId, MaterialCacheDTO> MaterialNameContainer = new Dictionary<ElementId, MaterialCacheDTO>();

        static readonly Lazy<Dictionary<string, MaterialConfig>> MatConfig = new(() => MaterialConfig.ReadFile());

        /// <summary>
        /// Export Revit materials.
        /// </summary>
        public static GLTFMaterial Export(MaterialNode node,
            Preferences preferences, Document doc)
        {
            GLTFMaterial gl_mat = new GLTFMaterial();
            float opacity = ONEINTVALUE - (float)node.Transparency;

            Material material = null;

            if (!MaterialNameContainer.TryGetValue(node.MaterialId, out var materialElement))
            {
                material = doc.GetElement(node.MaterialId) as Material;

                if (material == null)
                {
                    return gl_mat;
                }

                gl_mat.name = material.Name;
                gl_mat.UniqueId = material.UniqueId;
                MaterialNameContainer.Add(node.MaterialId, new MaterialCacheDTO(material.Name, material.UniqueId));
            }
            else
            {
                var elementData = MaterialNameContainer[node.MaterialId];
                gl_mat.name = elementData.MaterialName;
                gl_mat.UniqueId = elementData.UniqueId;
                material = doc.GetElement(node.MaterialId) as Material;
            }

            MatConfig.Value.TryGetValue(gl_mat.name, out var config);

            GLTFPBR pbr = new GLTFPBR();
            MaterialProperties.SetProperties(node, opacity, ref pbr, ref gl_mat, config);

            if (material != null && preferences.materials == MaterialsEnum.textures)
            {
                MaterialTextures.SetMaterialTextures(material, gl_mat, doc, opacity);
            }

            MaterialProperties.SetMaterialColour(node, opacity, ref pbr, ref gl_mat, config);

            return gl_mat;
        }
    }
}

