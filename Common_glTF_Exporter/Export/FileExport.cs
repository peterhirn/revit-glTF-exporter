﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Common_glTF_Exporter.Core;
using Common_glTF_Exporter.Windows.MainWindow;
using Newtonsoft.Json;
using Revit_glTF_Exporter;

namespace Common_glTF_Exporter.Export
{
    public static class FileExport
    {
        const string BIN = ".bin";
        const string GLTF = ".gltf";

        public static void Run(
            Preferences preferences,
            List<GLTFBufferView> bufferViews,
            List<GLTFBuffer> buffers,
            List<GLTFBinaryData> binaryFileData, List<GLTFScene> scenes,
            IndexedDictionary<GLTFNode> nodes,
            IndexedDictionary<GLTFMesh> meshes,
            IndexedDictionary<GLTFMaterial> materials,
            List<GLTFAccessor> accessors)
        {
            if (preferences.format == FormatEnum.gltf)
            {
                BufferConfig.Run(bufferViews, buffers, preferences);
                string fileDirectory = string.Concat(preferences.path, BIN);
                BinFile.Create(fileDirectory, binaryFileData, preferences.normals, preferences.batchId);

                string gltfJson = GltfJson.Get(scenes, nodes.List, meshes.List, materials.List, buffers,
                bufferViews, accessors, preferences);

                string gltfName = string.Concat(preferences.path, GLTF);
                File.WriteAllText(gltfName, gltfJson, Encoding.UTF8);
            }
            else
            {
                BufferConfig.Run(bufferViews, buffers, preferences);

                string gltfJson = GltfJson.Get(scenes, nodes.List, meshes.List, materials.List, buffers,
                bufferViews, accessors, preferences);

                GlbFile.Create(preferences, binaryFileData, gltfJson);
            }

        }
    }
}
