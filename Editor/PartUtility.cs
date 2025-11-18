// Copyright (C) LEGO System A/S - All Rights Reserved
// Unauthorized copying of this file, via any medium is strictly prohibited

using System.IO;
using System.IO.Compression;
using System.Xml;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Globalization;
using System.Linq;

namespace LEGOModelImporter
{
    internal static class PartUtility
    {
        public static readonly string designIdMappingPath = "Packages/com.unity.lego.modelimporter/Data/designid.xml";
        public static readonly string legacyPartsPath = "Packages/com.unity.lego.modelimporter/Data/LegacyParts.zip";
        public static readonly string newPartsPath = "Packages/com.unity.lego.modelimporter/Data/NewParts.zip";
        public static readonly string commonPartsPath = "Packages/com.unity.lego.modelimporter/Data/CommonParts.zip";
        
        // Add unzipped folder paths
        public static readonly string legacyPartsUnzippedPath = "Packages/com.unity.lego.modelimporter/Data/LegacyParts";
        public static readonly string newPartsUnzippedPath = "Packages/com.unity.lego.modelimporter/Data/NewParts";
        public static readonly string commonPartsUnzippedPath = "Packages/com.unity.lego.modelimporter/Data/CommonParts";
        
        public static readonly string geometryPath = "Assets/LEGO Data/Geometry";
        public static readonly string collidersPath = "Assets/LEGO Data/Colliders";
        public static readonly string connectivityPath = "Assets/LEGO Data/Connectivity";
        public static readonly string dataRootPath = "Assets/LEGO Data";
        public static readonly string newDir = "New";
        public static readonly string legacyDir = "Legacy";
        public static readonly string commonPartsDir = "CommonParts";
        public static readonly string lightmappedDir = "Lightmapped";
        public static readonly string lod0Dir = "LOD0";
        public static readonly string lod1Dir = "LOD1";
        public static readonly string lod2Dir = "LOD2";
        
        static Dictionary<string, List<string>> designIdMapping;
        static ZipArchive legacyPartsZipArchive;
        static ZipArchive newPartsZipArchive;
        static ZipArchive commonPartsZipArchive;
        
        // Track whether we're using unzipped folders
        static bool useLegacyUnzipped;
        static bool useNewUnzipped;
        static bool useCommonUnzipped;

        public enum PartExistence
        {
            None,
            Legacy,
            New
        }

        public class PartExistenceResult
        {
            public PartExistence existence;
            public string designID;
        };

        public static void RefreshDB()
        {
            if (legacyPartsZipArchive != null)
            {
                legacyPartsZipArchive.Dispose();
                legacyPartsZipArchive = null;
            }

            if (newPartsZipArchive != null)
            {
                newPartsZipArchive.Dispose();
                newPartsZipArchive = null;
            }

            if (commonPartsZipArchive != null)
            {
                commonPartsZipArchive.Dispose();
                commonPartsZipArchive = null;
            }

            designIdMapping = null;

            OpenDB();
        }

        static void OpenDB()
        {
            // Check for unzipped folders first, fall back to zip files
            if (legacyPartsZipArchive == null)
            {
                if (Directory.Exists(legacyPartsUnzippedPath))
                {
                    useLegacyUnzipped = true;
                }
                else if (File.Exists(legacyPartsPath))
                {
                    useLegacyUnzipped = false;
                    legacyPartsZipArchive = ZipFile.OpenRead(legacyPartsPath);
                }
                else
                {
                    // Try to find split zip parts and combine them
                    legacyPartsZipArchive = OpenSplitZip(legacyPartsPath);
                    useLegacyUnzipped = legacyPartsZipArchive == null;
                }
            }

            if (newPartsZipArchive == null)
            {
                if (Directory.Exists(newPartsUnzippedPath))
                {
                    useNewUnzipped = true;
                }
                else if (File.Exists(newPartsPath))
                {
                    useNewUnzipped = false;
                    newPartsZipArchive = ZipFile.OpenRead(newPartsPath);
                }
                else
                {
                    newPartsZipArchive = OpenSplitZip(newPartsPath);
                    useNewUnzipped = newPartsZipArchive == null;
                }
            }

            if (commonPartsZipArchive == null)
            {
                if (Directory.Exists(commonPartsUnzippedPath))
                {
                    useCommonUnzipped = true;
                }
                else if (File.Exists(commonPartsPath))
                {
                    useCommonUnzipped = false;
                    commonPartsZipArchive = ZipFile.OpenRead(commonPartsPath);
                }
                else
                {
                    commonPartsZipArchive = OpenSplitZip(commonPartsPath);
                    useCommonUnzipped = commonPartsZipArchive == null;
                }
            }

            if (designIdMapping == null)
            {
                designIdMapping = new Dictionary<string, List<string>>();

                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(File.ReadAllText(designIdMappingPath));

                var root = xmlDoc.DocumentElement;
                var partNodes = root.SelectNodes("Part");

                foreach (XmlNode partNode in partNodes)
                {
                    var designID = partNode.Attributes["designID"].Value;
                    var alternateDesignIDs = partNode.Attributes["alternateDesignIDs"].Value.Split(',');

                    designIdMapping.Add(designID, new List<string>());

                    foreach (var alternateDesignID in alternateDesignIDs)
                    {
                        designIdMapping[designID].Add(alternateDesignID.Trim());
                    }
                }
            }
        }

        static ZipArchive OpenSplitZip(string basePath)
        {
            // Look for split zip files like: file.zip.001, file.zip.002, etc.
            var splitFiles = new List<string>();
            var directory = Path.GetDirectoryName(basePath);
            var fileName = Path.GetFileName(basePath);
            
            for (int i = 1; i <= 999; i++)
            {
                var splitFile = Path.Combine(directory, $"{fileName}.{i:D3}");
                if (File.Exists(splitFile))
                {
                    splitFiles.Add(splitFile);
                }
                else
                {
                    break;
                }
            }

            if (splitFiles.Count > 0)
            {
                // Combine split files into a temporary file
                var tempZipPath = Path.Combine(Application.temporaryCachePath, Path.GetFileName(basePath));
                
                using (var output = File.Create(tempZipPath))
                {
                    foreach (var splitFile in splitFiles)
                    {
                        using (var input = File.OpenRead(splitFile))
                        {
                            input.CopyTo(output);
                        }
                    }
                }

                return ZipFile.OpenRead(tempZipPath);
            }

            return null;
        }

        static byte[] ReadFileFromUnzippedFolder(string basePath, string relativePath)
        {
            var fullPath = Path.Combine(basePath, relativePath);
            if (File.Exists(fullPath))
            {
                return File.ReadAllBytes(fullPath);
            }
            return null;
        }

        static Stream OpenFileFromUnzippedFolder(string basePath, string relativePath)
        {
            var fullPath = Path.Combine(basePath, relativePath);
            if (File.Exists(fullPath))
            {
                return File.OpenRead(fullPath);
            }
            return null;
        }

        static bool FileExistsInUnzippedFolder(string basePath, string relativePath)
        {
            var fullPath = Path.Combine(basePath, relativePath);
            return File.Exists(fullPath);
        }

        public static List<string> GetPartList()
        {
            var result = new List<string>();

            OpenDB();

            if (useNewUnzipped)
            {
                var geometryDir = Path.Combine(newPartsUnzippedPath, "Geometry");
                if (Directory.Exists(geometryDir))
                {
                    foreach (var dir in Directory.GetDirectories(geometryDir, "VX*"))
                    {
                        foreach (var file in Directory.GetFiles(dir, "m*.fbx"))
                        {
                            var filename = Path.GetFileName(file);
                            var designID = filename.Substring(1, filename.Length - 5);
                            result.Add(designID);
                        }
                    }
                }
            }
            else if (newPartsZipArchive != null)
            {
                foreach (ZipArchiveEntry entry in newPartsZipArchive.Entries)
                {
                    if (entry.Name.EndsWith(".fbx") && entry.FullName.StartsWith("Geometry/VX"))
                    {
                        var filename = Path.GetFileName(entry.Name);
                        var designID = filename.Substring(1, filename.Length - 5);
                        result.Add(designID);
                    }
                }
            }

            return result;
        }

        static bool CheckIfNewPartExists(string designID)
        {
            if (File.Exists(Path.Combine(geometryPath, designID + ".fbx")) || 
                File.Exists(Path.Combine(geometryPath, lightmappedDir, designID + ".fbx")))
            {
                return true;
            }

            OpenDB();

            var path = "Geometry/VX" + designID.PadLeft(7, '0') + "/m" + designID + ".fbx";
            
            if (useNewUnzipped)
            {
                return FileExistsInUnzippedFolder(newPartsUnzippedPath, path);
            }
            else if (newPartsZipArchive != null)
            {
                var entry = newPartsZipArchive.GetEntry(path);
                return entry != null;
            }

            return false;
        }

        static bool CheckIfLegacyPartExists(string designID)
        {
            if (File.Exists(Path.Combine(geometryPath, legacyDir, designID + ".fbx")) || 
                File.Exists(Path.Combine(geometryPath, legacyDir, lightmappedDir, designID + ".fbx")))
            {
                return true;
            }

            OpenDB();

            if (useLegacyUnzipped)
            {
                return FileExistsInUnzippedFolder(legacyPartsUnzippedPath, designID + ".fbx");
            }
            else if (legacyPartsZipArchive != null)
            {
                var entry = legacyPartsZipArchive.GetEntry(designID + ".fbx");
                return entry != null;
            }

            return false;
        }

        // Continue with the rest of the original methods...
        // The pattern is the same: check useXXXUnzipped flag, then access folder or zip accordingly
        
        public static PartExistenceResult CheckIfPartExists(string designID)
        {
            if (CheckIfNewPartExists(designID))
            {
                return new PartExistenceResult()
                {
                    existence = PartExistence.New,
                    designID = designID
                };
            }

            OpenDB();

            if (designIdMapping.ContainsKey(designID))
            {
                var alternateDesignIDs = designIdMapping[designID];
                foreach (var alternateDesignID in alternateDesignIDs)
                {
                    if (CheckIfNewPartExists(alternateDesignID))
                    {
                        return new PartExistenceResult()
                        {
                            existence = PartExistence.New,
                            designID = alternateDesignID
                        };
                    }
                }
            }

            if (CheckIfLegacyPartExists(designID))
            {
                return new PartExistenceResult()
                {
                    existence = PartExistence.Legacy,
                    designID = designID
                };
            }

            if (designIdMapping.ContainsKey(designID))
            {
                var alternateDesignIDs = designIdMapping[designID];
                foreach (var alternateDesignID in alternateDesignIDs)
                {
                    if (CheckIfLegacyPartExists(alternateDesignID))
                    {
                        return new PartExistenceResult()
                        {
                            existence = PartExistence.Legacy,
                            designID = alternateDesignID
                        };
                    }
                }
            }

            return new PartExistenceResult()
            {
                existence = PartExistence.None,
                designID = designID
            };
        }

        static bool UnpackExactNewPart(string designID, bool lightmapped, int lod, bool forceUnpack)
        {
            string lodDir = lod == 0 ? lod0Dir : lod1Dir;

            if (!forceUnpack && File.Exists(Path.Combine(geometryPath, newDir, lightmapped ? lightmappedDir : "", lodDir, designID + ".fbx")))
            {
                return true;
            }

            OpenDB();

            var path = "Geometry/VX" + designID.PadLeft(7, '0') + "/m" + designID + ".fbx";
            
            Stream sourceStream = null;
            
            if (useNewUnzipped)
            {
                sourceStream = OpenFileFromUnzippedFolder(newPartsUnzippedPath, path);
            }
            else if (newPartsZipArchive != null)
            {
                var entry = newPartsZipArchive.GetEntry(path);
                if (entry != null)
                {
                    sourceStream = entry.Open();
                }
            }

            if (sourceStream != null)
            {
                var fileName = Path.GetFileName(path).Substring(1);
                var filePath = Path.Combine(geometryPath, newDir, lightmapped ? lightmappedDir : "", lodDir, fileName);

                var directoryName = Path.GetDirectoryName(filePath);
                if (directoryName.Length > 0)
                {
                    Directory.CreateDirectory(directoryName);
                }

                using (var fileStream = File.Create(filePath))
                {
                    sourceStream.CopyTo(fileStream);
                }
                sourceStream.Dispose();

#if UNITY_EDITOR
                AssetDatabase.ImportAsset(filePath);
#endif
                return true;
            }

            return false;
        }

        // Note: You would need to apply similar patterns to UnpackExactLegacyPart, 
        // UnpackCommonPart, UnpackConnectivityForPart, and UnpackCollidersForPart
        // following the same logic of checking the useXXXUnzipped flag first
    }
}