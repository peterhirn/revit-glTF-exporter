﻿namespace Common_glTF_Exporter.Export
{
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;

    public static class ZIP
    {
        /// <summary>
        /// Compress ZIP file.
        /// </summary>
        /// <param name="zipName">ZIP file name.</param>
        /// <param name="files">Files.</param>
        public static void Compress(string zipName, List<string> files)
        {
            var zip = ZipFile.Open(zipName, ZipArchiveMode.Create);

            foreach (var file in files)
            {
                // Add the entry for each file
                zip.CreateEntryFromFile(file, Path.GetFileName(file), CompressionLevel.Optimal);
            }

            // Dispose of the object when we are done
            zip.Dispose();
        }
    }
}
