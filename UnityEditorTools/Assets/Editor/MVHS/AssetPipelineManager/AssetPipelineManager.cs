using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MVHS
{
    /// <summary>
    /// Editor window for exporting and importing asset packages.
    ///
    /// Export: Gathers all assets from configured folders, bundles them into a
    ///         .unitypackage, and generates a manifest file that records what
    ///         was exported. The manifest is committed to git so teammates
    ///         know what assets to expect after importing.
    ///
    /// Import: Lets a student browse for a .unitypackage and import it.
    ///
    /// The manifest system allows multiple students to export independently
    /// without merge conflicts — each export creates a new manifest file.
    ///
    /// Usage: Tools > Asset Package Manager
    /// </summary>
    public class AssetPipelineManager : EditorWindow
    {
        public struct FolderStatus
        {
            public string path;
            public FolderState state;
            public int diskFileCount;
            public int expectedFileCount;
        }

        //private AssetPipelineConfig m_Config;
        private Vector2 m_ScrollPosition;

        // Export fields.
        private string m_ExporterName = "";
        private string m_ExportDescription = "";

        // Cached state.
        private List<FolderStatus> m_FolderStatuses = new List<FolderStatus>();
        private Dictionary<string, List<string>> m_ExpectedFiles = new Dictionary<string, List<string>>();

        public enum FolderState
        {
            Ready,              // Has assets on disk — good to go.
            NeedsImport,        // Manifest says it should have files, but they're missing.
            EmptyNoManifest,    // Empty and no manifest mentions it — nobody has added assets yet.
            Missing             // Folder doesn't exist on disk at all.
        }


        [MenuItem("Tools/MVHS/Asset Pipeline Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<AssetPipelineManager>("Asset Pipeline Manager");
            window.minSize = new Vector2(450, 550);
        }

        private void OnEnable()
        {
            LoadExporterName();
            Refresh();
        }

        private void OnFocus()
        {
            Refresh();
        }


        private void Refresh()
        {
            LoadCombinedManifest();
            RefreshFolderStatuses();
        }

        // -------------------------------------------------------------------------
        // Manifest Loading — combines all manifest files into one picture
        // -------------------------------------------------------------------------

        private void LoadCombinedManifest()
        {
            m_ExpectedFiles.Clear();
            string diskManifestPath = AssetPipelineConfig.GetDiskPath(AssetPipelineConfig.GetManifestFolderAssetPath());


            if (!Directory.Exists(diskManifestPath))
            {
                return;
            }

            string[] manifestFiles = Directory.GetFiles(diskManifestPath, "*.json");

            foreach (string file in manifestFiles)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var manifest = JsonUtility.FromJson<AssetPipelineManifest>(json);

                    if (manifest?.folders == null) continue;

                    foreach (var folder in manifest.folders)
                    {
                        if (!m_ExpectedFiles.ContainsKey(folder.folderPath))
                        {
                            m_ExpectedFiles[folder.folderPath] = new List<string>();
                        }

                        // Merge file lists — union of all manifests.
                        foreach (string f in folder.files)
                        {
                            if (!m_ExpectedFiles[folder.folderPath].Contains(f))
                            {
                                m_ExpectedFiles[folder.folderPath].Add(f);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AssetPipelineManager] Could not read manifest {file}: {e.Message}");
                }
            }
        }

        private static bool AssetPathExists(string assetPath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string fullPath = Path.Combine(projectRoot, assetPath);
            return File.Exists(fullPath);
        }

        private void RefreshFolderStatuses()
        {
            m_FolderStatuses.Clear();

            foreach (string folder in AssetPipelineConfig.projectFolders)
            {
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                string diskPath = Path.Combine(projectRoot, folder);
                diskPath = diskPath.Replace("\\", "/");

                bool existsOnDisk = Directory.Exists(diskPath);
                int diskCount = 0;

                if (existsOnDisk)
                {
                    string[] files = Directory.GetFiles(diskPath, "*.*", SearchOption.AllDirectories);
                    foreach (string file in files)
                    {
                        if (!file.EndsWith(".meta"))
                        {
                            diskCount++;
                        }
                    }
                }

                bool hasManifestEntry = m_ExpectedFiles.ContainsKey(folder);
                int expectedCount = hasManifestEntry ? m_ExpectedFiles[folder].Count : 0;

                int missingCount = 0;

                if (hasManifestEntry)
                {
                    foreach (string expectedFile in m_ExpectedFiles[folder])
                    {
                        if (!AssetPathExists(expectedFile))
                        {
                            missingCount++;
                        }
                    }
                }

                FolderState state;

                if (hasManifestEntry && missingCount > 0)
                {
                    state = FolderState.NeedsImport;
                }
                else if (diskCount > 0)
                {
                    state = FolderState.Ready;
                }
                else if (!existsOnDisk)
                {
                    state = FolderState.Missing;
                }
                else
                {
                    state = FolderState.EmptyNoManifest;
                }

                m_FolderStatuses.Add(new FolderStatus
                {
                    //path = fullPath,
                    path = folder,
                    state = state,
                    diskFileCount = diskCount,
                    expectedFileCount = expectedCount
                });
            }
        }

        // -------------------------------------------------------------------------
        // UI Drawing
        // -------------------------------------------------------------------------

        private void OnGUI()
        {
            m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);

            DrawHeader();

            DrawFolderOverview();
            EditorGUILayout.Space(10);
            DrawExportSection();
            EditorGUILayout.Space(10);
            DrawImportSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Asset Pipeline Manager", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Export asset folders into a .unitypackage for distribution, " +
                "or import a package received from a teammate.",
                MessageType.Info);
            EditorGUILayout.Space(5);
        }


        private void DrawFolderOverview()
        {
            EditorGUILayout.LabelField("Folder Status", EditorStyles.boldLabel);

            // Legend.
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("●  Has assets", EditorStyles.miniLabel, GUILayout.Width(100));
            EditorGUILayout.LabelField("⬇  Needs import", EditorStyles.miniLabel, GUILayout.Width(100));
            EditorGUILayout.LabelField("○  No assets yet", EditorStyles.miniLabel, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            foreach (var status in m_FolderStatuses)
            {
                EditorGUILayout.BeginHorizontal();

                string icon;
                string tooltip;
                string countLabel;

                switch (status.state)
                {
                    case FolderState.Ready:
                        icon = "●";
                        tooltip = "Assets present on disk";
                        countLabel = status.diskFileCount + " file(s)";
                        break;

                    case FolderState.NeedsImport:
                        icon = "⬇";
                        tooltip = "Manifest expects " + status.expectedFileCount +
                                  " file(s) — import the asset package";
                        countLabel = "need " + $"{status.expectedFileCount - status.diskFileCount}";
                        break;

                    case FolderState.EmptyNoManifest:
                        icon = "○";
                        tooltip = "No assets here yet and no manifest references it";
                        countLabel = "unused";
                        break;

                    case FolderState.Missing:
                    default:
                        icon = "○";
                        tooltip = "Folder does not exist yet — no one has added assets here";
                        countLabel = "unused";
                        break;
                }

                EditorGUILayout.LabelField(new GUIContent(icon, tooltip), GUILayout.Width(20));
                EditorGUILayout.LabelField(status.path);
                EditorGUILayout.LabelField(countLabel, EditorStyles.miniLabel, GUILayout.Width(80));

                EditorGUILayout.EndHorizontal();
            }

            // Summary warning if anything needs import.
            int needsImportCount = m_FolderStatuses.Count(s => s.state == FolderState.NeedsImport);
            if (needsImportCount > 0)
            {
                EditorGUILayout.Space(3);
                EditorGUILayout.HelpBox(
                    needsImportCount + " folder(s) have assets in the manifest but not on disk. " +
                    "Import the asset package before saving any scenes or prefabs.",
                    MessageType.Warning);
            }
        }

        private void DrawExportSection()
        {
            EditorGUILayout.LabelField("Export", EditorStyles.boldLabel);

            m_ExporterName = EditorGUILayout.TextField(
                new GUIContent("Your Name", "Used in the manifest filename to avoid conflicts"),
                m_ExporterName);

            m_ExportDescription = EditorGUILayout.TextField(
                new GUIContent("Description", "Brief note about what assets changed (optional)"),
                m_ExportDescription);

            bool hasAssets = m_FolderStatuses.Any(s => s.state == FolderState.Ready);
            bool hasName = !string.IsNullOrWhiteSpace(m_ExporterName);
            bool hasDescription = !string.IsNullOrWhiteSpace(m_ExportDescription);

            EditorGUI.BeginDisabledGroup(!hasAssets || !hasName || !hasDescription);
            if (GUILayout.Button("Export Asset Package", GUILayout.Height(30)))
            {
                SaveExporterName();
                ExportPackage();
            }
            EditorGUI.EndDisabledGroup();

            if (!hasName)
            {
                EditorGUILayout.HelpBox("Enter your name before exporting.", MessageType.Info);
            }
            else if (!hasAssets)
            {
                EditorGUILayout.HelpBox("No folders contain assets to export.", MessageType.Info);
            }
            else if (!hasDescription)
            {
                EditorGUILayout.HelpBox("Enter a description before exporting.", MessageType.Info);
            }
        }

        private void DrawImportSection()
        {
            EditorGUILayout.LabelField("Import", EditorStyles.boldLabel);

            if (GUILayout.Button("Import Asset Package", GUILayout.Height(30)))
            {
                ImportPackage();
            }
        }

        // -------------------------------------------------------------------------
        // Export Logic
        // -------------------------------------------------------------------------

        private void ExportPackage()
        {
            HashSet<string> assetPaths = new HashSet<string>();
            var manifestFolders = new List<ManifestFolder>();

            //foreach (string folder in m_Config.assetFolders)
            foreach (string folder in AssetPipelineConfig.projectFolders)
            {
                //string fullPath = m_Config.GetFullPath(folder);
                string fullPath = folder;

                if (!AssetDatabase.IsValidFolder(fullPath))
                {
                    continue;
                }

                string[] guids = AssetDatabase.FindAssets("", new[] { fullPath });
                var folderFiles = new List<string>();

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetDatabase.IsValidFolder(path))
                    {
                        continue;
                    }

                    assetPaths.Add(path);
                    folderFiles.Add(path);
                }

            }

            Dictionary<string, List<string>> tempManifestBuilder = new Dictionary<string, List<string>>();
            foreach (string assetPath in assetPaths)
            {
                string path = Path.GetDirectoryName(assetPath);
                path = path.Replace("\\", "/");
                path += "/";
                bool hasKey = tempManifestBuilder.TryGetValue(path, out var key);
                if (hasKey)
                {
                    key.Add(assetPath);
                }
                else
                {
                    List<string> s = new List<string>();
                    s.Add(assetPath);
                    tempManifestBuilder.Add(path, s);
                }
            }


            foreach (var kvp in tempManifestBuilder)
            {
                manifestFolders.Add(new ManifestFolder
                {
                    folderPath = kvp.Key,
                    files = kvp.Value
                });
            }


            if (assetPaths.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Nothing to Export",
                    "No assets found in the configured folders.",
                    "OK");
                return;
            }

            // Ask where to save the .unitypackage.
            string defaultSaveName = $"{m_ExporterName.Trim()}_{DateTime.Now.ToString("yyyy_MM_ddTHH_mm_ss")}";
         
            string defaultDir = Path.GetDirectoryName(Application.dataPath);
            string savePath = EditorUtility.SaveFilePanel(
                "Save Asset Package",
                defaultDir,
                defaultSaveName,
                "unitypackage");

            if (string.IsNullOrEmpty(savePath)) return;

            // Export the package.
            AssetDatabase.ExportPackage(
                assetPaths.ToArray(),
                savePath,
                ExportPackageOptions.Recurse);

            // Generate the manifest.
            GenerateManifest(manifestFolders);

            long fileSize = new FileInfo(savePath).Length;
            string sizeLabel = fileSize > 1048576
                ? (fileSize / 1048576f).ToString("F1") + " MB"
                : (fileSize / 1024f).ToString("F0") + " KB";

            EditorUtility.DisplayDialog(
                "Export Complete",
                $"Exported {assetPaths.Count} asset(s) to:\n{savePath}\n\n" +
                $"Package size: {sizeLabel}\n\n" +
                "A manifest file has been generated.\n" +
                "Remember to commit the manifest to git!",
                "OK");

            Debug.Log($"[AssetPipelineManager] Exported {assetPaths.Count} assets ({sizeLabel}). Manifest generated.");

            Refresh();
        }

        // -------------------------------------------------------------------------
        // Manifest Generation
        // -------------------------------------------------------------------------

        private void GenerateManifest(List<ManifestFolder> folders)
        {
            // Ensure the manifest folder exists.
            string manifestFolderAssetPath = AssetPipelineConfig.GetManifestFolderAssetPath(); //AssetPipelineConfig.ManifestFolderPath; //m_Config.GetManifestFolderPath();
            EnsureFolderExists(manifestFolderAssetPath);

            // Build the manifest.
            var manifest = new AssetPipelineManifest
            {
                exportedBy = m_ExporterName.Trim(),
                exportDate = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                description = m_ExportDescription.Trim(),
                folders = folders
            };

            // Create a unique filename: manifest_Name_Date.json
            string safeName = SanitizeFilename(m_ExporterName.Trim());
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string fileName = $"manifest_{safeName}_{timestamp}.json";

            string projectRoot = Path.GetDirectoryName(Application.dataPath);

            string fullFilePath = Path.Combine(
                projectRoot,
                //AssetPipelineConfig.ManifestFolderPath,
                manifestFolderAssetPath,
                fileName
                );

            fullFilePath = fullFilePath.Replace("\\", "/");

            string json = JsonUtility.ToJson(manifest, true);
            File.WriteAllText(fullFilePath, json);

            // Tell Unity about the new file.
            AssetDatabase.Refresh();

            Debug.Log($"[AssetPipelineManager] Manifest saved: {fileName}");
        }

        // -------------------------------------------------------------------------
        // Import Logic
        // -------------------------------------------------------------------------

        private void ImportPackage()
        {
            string openPath = EditorUtility.OpenFilePanel(
                "Select Asset Package",
                "",
                "unitypackage");

            if (string.IsNullOrEmpty(openPath)) return;

            AssetDatabase.ImportPackage(openPath, true);

            Debug.Log($"[AssetPipelineManager] Import started from {openPath}");
        }


        // -------------------------------------------------------------------------
        // Utility
        // -------------------------------------------------------------------------

        private void EnsureFolderExists(string assetPath)
        {
            // Walk down the path creating folders as needed.
            string[] parts = assetPath.Split('/');
            string current = parts[0]; // "Assets"

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private string SanitizeFilename(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char c in invalid)
            {
                name = name.Replace(c, '_');
            }
            return name.Replace(' ', '_');
        }

        private void SaveExporterName()
        {
            EditorPrefs.SetString("AssetPackageManager_ExporterName", m_ExporterName);
        }

        private void LoadExporterName()
        {
            m_ExporterName = EditorPrefs.GetString("AssetPackageManager_ExporterName", "");
        }
    }
}