using System;
using System.IO;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using DefaultNamespace.Data;
using DefaultNamespace.MessageBox;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace Version
{
    public class VersionManager : MonoBehaviour
    {
        static bool IsMobilePlatform =>
            Application.platform == RuntimePlatform.IPhonePlayer ||
            Application.platform == RuntimePlatform.Android;

        string _outputPath = "";
        bool _showLocalInstallPrompt;
        string _localInstallInput = "";
        string _localInstallPromptError = "";
        UniTaskCompletionSource<string> _localInstallPromptTcs;

        public MessageBox messageBox;
        public GameObject failedPanel;

        public class GitHubFileInfo
        {
            public string Name { get; set; }
            public string Path { get; set; }
            public string SHA { get; set; }
            public int Size { get; set; }
            public string URL { get; set; }
            public string HtmlURL { get; set; }
            public string GitURL { get; set; }
            public string DownloadURL { get; set; }
            public string Type { get; set; }
            public string Content { get; set; }
            public string Encoding { get; set; }
        }

        async void Start()
        {
            DontDestroyOnLoad(gameObject);
            try
            {
                SeerResources.EnsureUserDataRootExists();
                _outputPath = SeerResources.UserDataFile(SeerResources.MonstersFile);

                if (!IsMobilePlatform)
                {
                    if (await TryBootstrapFromLocalAsync())
                    {
                        LoadLeaderboardScene();
                        return;
                    }
                }
                else
                {
                    Debug.Log("[VersionManager] Mobile platform detected. Skipping local install search.");
                }

                await ContinueMirrorStartupAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VersionManager] Startup failed.\n{ex}");
                OnDownloadFailed().Forget();
            }
        }

        async UniTask<bool> TryBootstrapFromLocalAsync()
        {
            var reader = new LocalUnityAssetReader();
            if (reader.IsAvailable)
            {
                Debug.Log(reader.DescribeStatus());
                if (EnsureLocalConfigReady(reader) && RefreshMonstersData())
                {
                    Debug.Log("[VersionManager] Loaded config from the local Seer source root.");
                    return true;
                }
            }
            else
            {
                Debug.Log("[VersionManager] No usable local Seer source root was detected. Prompting for a folder.");
            }

            var pickedPath = await PromptForLocalInstallPathAsync();
            if (string.IsNullOrEmpty(pickedPath))
            {
                return false;
            }

            SeerResources.LocalInstallPath = pickedPath;
            var selectedReader = new LocalUnityAssetReader(pickedPath);
            if (!selectedReader.IsAvailable)
            {
                return false;
            }

            Debug.Log(selectedReader.DescribeStatus());
            if (EnsureLocalConfigReady(selectedReader) && RefreshMonstersData())
            {
                Debug.Log("[VersionManager] Loaded config from the user-selected Seer source root.");
                return true;
            }

            return false;
        }

        bool EnsureLocalConfigReady(LocalUnityAssetReader reader)
        {
            var monstersPath = SeerResources.UserDataFile(SeerResources.MonstersFile);
            var skinPath = SeerResources.UserDataFile(SeerResources.PetSkinFile);
            var configVersion = reader.GetConfigPackageVersion();
            var installPath = reader.InstallRoot;

            var cachedVersion = PlayerPrefs.GetString(SeerResources.LocalConfigVersionPrefKey, "");
            var cachedInstallPath = SeerResources.NormalizePath(
                PlayerPrefs.GetString(SeerResources.LocalConfigInstallPrefKey, ""));

            var needsExport =
                !File.Exists(monstersPath)
                || !File.Exists(skinPath)
                || !string.Equals(cachedVersion, configVersion, StringComparison.Ordinal)
                || !string.Equals(cachedInstallPath, installPath, StringComparison.OrdinalIgnoreCase);

            if (!needsExport)
            {
                return true;
            }

            var exporter = new SeerLocalConfigExporter(reader);
            var result = exporter.ExportToPersistentData();
            if (!result.Success)
            {
                Debug.LogWarning(
                    "[VersionManager] Local Unity export failed. Falling back to existing cache or mirror.\n" +
                    result.Error);
                return File.Exists(monstersPath);
            }

            PlayerPrefs.SetString(SeerResources.LocalConfigVersionPrefKey, configVersion);
            PlayerPrefs.SetString(SeerResources.LocalConfigInstallPrefKey, installPath);
            PlayerPrefs.SetString(SeerResources.LocalDefaultManifestVersionPrefKey, reader.GetDefaultPackageVersion());
            PlayerPrefs.Save();

            Debug.Log(
                $"[VersionManager] Local config export completed. monsters={result.MonsterCount}, " +
                $"configVersion={result.ConfigVersion}, petSkinSource={result.PetSkinSource}");
            return true;
        }

        async UniTask<string> PromptForLocalInstallPathAsync()
        {
            if (IsMobilePlatform)
            {
                Debug.Log("[VersionManager] Mobile: skipping folder picker prompt.");
                return "";
            }

            _localInstallInput = SeerResources.LocalInstallPath;
            if (string.IsNullOrEmpty(_localInstallInput))
            {
                _localInstallInput = SeerResources.FindBestLocalInstall();
            }

            if (string.IsNullOrEmpty(_localInstallInput))
            {
                _localInstallInput = @"D:\SeerLauncher\games\NewSeer";
            }

            _localInstallPromptError = "";
            _showLocalInstallPrompt = true;
            _localInstallPromptTcs = new UniTaskCompletionSource<string>();
            return await _localInstallPromptTcs.Task;
        }

        void BrowseLocalInstallPrompt()
        {
            var initialPath = _localInstallInput;
            if (string.IsNullOrWhiteSpace(initialPath))
            {
                initialPath = SeerResources.FindBestLocalInstall();
            }

            if (string.IsNullOrWhiteSpace(initialPath))
            {
                initialPath = @"D:\";
            }

            if (!WindowsFolderPicker.TryPickFolder("Choose the NewSeer install or mirror root", initialPath, out var folderPath))
            {
                if (Application.platform == RuntimePlatform.WindowsPlayer ||
                    Application.platform == RuntimePlatform.WindowsEditor)
                {
                    _localInstallPromptError = "No folder was selected.";
                }
                else
                {
                    _localInstallPromptError = "Folder browsing is only available on Windows.";
                }

                return;
            }

            _localInstallInput = folderPath;
            _localInstallPromptError = "";
        }

        void ConfirmLocalInstallPrompt()
        {
            var normalized = SeerResources.NormalizePath(_localInstallInput);
            if (!SeerResources.LooksLikeLocalInstall(normalized))
            {
                _localInstallPromptError =
                    "Invalid folder. It must contain Seer_Data/yoo plus package manifest files.";
                return;
            }

            CloseLocalInstallPrompt(normalized);
        }

        void CloseLocalInstallPrompt(string result)
        {
            _showLocalInstallPrompt = false;
            _localInstallPromptError = "";

            var tcs = _localInstallPromptTcs;
            _localInstallPromptTcs = null;
            tcs?.TrySetResult(result);
        }

        async UniTask ContinueMirrorStartupAsync()
        {
            var sha = await GetFileLastSHA(
                SeerResources.ConfigRepoOwner,
                SeerResources.ConfigRepoName,
                SeerResources.ConfigRepoMonstersPath);

            var localSHA = PlayerPrefs.HasKey("localSHA")
                ? PlayerPrefs.GetString("localSHA")
                : "";

            var remoteAvailable = !string.IsNullOrEmpty(sha);
            if (!remoteAvailable)
            {
                if (File.Exists(_outputPath) && RefreshMonstersData())
                {
                    Debug.LogWarning("[VersionManager] Unable to verify the remote mirror. Using cached local data.");
                    LoadLeaderboardScene();
                    return;
                }

                Debug.LogError("[VersionManager] Unable to reach the remote mirror and no local cache is available.");
                OnDownloadFailed().Forget();
                return;
            }

            if (sha != localSHA || !File.Exists(_outputPath))
            {
                if (messageBox != null)
                {
                    messageBox.Setup(
                        "Mirror Update",
                        "A newer mirror config was detected. Update now?\n" +
                        "If you have a local client or a synced mirror root, local export is preferred.",
                        () => Download(sha).Forget(),
                        () => UseCacheOrDownloadAsync(sha).Forget());
                    return;
                }

                await UseCacheOrDownloadAsync(sha);
                return;
            }

            Debug.Log("[VersionManager] Mirror config is unchanged. Using cached files.");
            if (RefreshMonstersData())
            {
                LoadLeaderboardScene();
                return;
            }

            await Download(sha);
        }

        async UniTask UseCacheOrDownloadAsync(string sha)
        {
            if (!PlayerPrefs.HasKey("firstOpen") || !File.Exists(_outputPath))
            {
                var result = await Download(sha);
                if (result)
                {
                    PlayerPrefs.SetInt("firstOpen", 1);
                }
                return;
            }

            if (RefreshMonstersData())
            {
                LoadLeaderboardScene();
                return;
            }

            await Download(sha);
        }

        async UniTask<string> GetFileLastSHA(string owner, string repo, string filePath)
        {
            var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/contents/{filePath}";

            using var request = UnityWebRequest.Get(apiUrl);
            request.SetRequestHeader("User-Agent", "Unity-App");
            request.timeout = 15;

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

#if UNITY_2020_1_OR_NEWER
            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.ProtocolError)
#else
            if (request.isNetworkError || request.isHttpError)
#endif
            {
                Debug.LogError($"[VersionManager] Failed to query GitHub file info: {request.error}");
                return null;
            }

            var fileInfo = JsonConvert.DeserializeObject<GitHubFileInfo>(request.downloadHandler.text);
            if (fileInfo == null)
            {
                return null;
            }

            Debug.Log($"[VersionManager] Remote monsters.json SHA: {fileInfo.SHA}");
            return fileInfo.SHA;
        }

        static async Task<bool> DownloadMirrorFileAsync(string fileName, int maxRetries = 3)
        {
            var outputPath = SeerResources.UserDataFile(fileName);
            var url = SeerResources.ConfigMirrorBase + fileName;

            for (var attempt = 0; attempt <= maxRetries; attempt++)
            {
                var request = UnityWebRequest.Get(url);
                request.timeout = 60;

                try
                {
                    var operation = request.SendWebRequest();
                    while (!operation.isDone)
                    {
                        await Task.Yield();
                    }

#if UNITY_2020_1_OR_NEWER
                    if (request.result == UnityWebRequest.Result.ConnectionError ||
                        request.result == UnityWebRequest.Result.ProtocolError)
#else
                    if (request.isNetworkError || request.isHttpError)
#endif
                    {
                        Debug.LogError(
                            $"[VersionManager] Failed to download {fileName} " +
                            $"({attempt + 1}/{maxRetries + 1}): {request.error}");
                        if (attempt < maxRetries)
                        {
                            await Task.Delay(1000 * (attempt + 1));
                        }
                        continue;
                    }

                    var fileBytes = request.downloadHandler.data;
                    await File.WriteAllBytesAsync(outputPath, fileBytes);
                    Debug.Log($"[VersionManager] Downloaded {fileName} -> {outputPath}");
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[VersionManager] Download exception for {fileName}: {ex.Message}");
                    return false;
                }
                finally
                {
                    request.Dispose();
                }
            }

            return false;
        }

        static Task<bool> DownloadMonstersJsonAsync(int maxRetries = 3) =>
            DownloadMirrorFileAsync(SeerResources.MonstersFile, maxRetries);

        static Task<bool> DownloadMonstersSkinJsonAsync(int maxRetries = 3) =>
            DownloadMirrorFileAsync(SeerResources.PetSkinFile, maxRetries);

        bool RefreshMonstersData()
        {
            try
            {
                var monstersPath = SeerResources.UserDataFile(SeerResources.MonstersFile);
                if (!File.Exists(monstersPath))
                {
                    return false;
                }

                var monstersJson = File.ReadAllText(monstersPath);
                MonstersData.MonstersRoot = JsonConvert.DeserializeObject<MonstersRoot>(monstersJson);
                if (MonstersData.MonstersRoot?.Monsters?.Monster == null)
                {
                    Debug.LogError("[VersionManager] Failed to parse monsters.json.");
                    return false;
                }

                var skinPath = SeerResources.UserDataFile(SeerResources.PetSkinFile);
                if (File.Exists(skinPath))
                {
                    var skinJson = File.ReadAllText(skinPath);
                    SkinData.PetSkinsRoot = JsonConvert.DeserializeObject<PetSkinsRoot>(skinJson)
                                            ?? SeerLocalConfigExporter.CreateEmptyPetSkinsRoot();
                }
                else
                {
                    SkinData.PetSkinsRoot = SeerLocalConfigExporter.CreateEmptyPetSkinsRoot();
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VersionManager] Failed to refresh local config cache.\n{ex}");
                return false;
            }
        }

        async UniTask<bool> Download(string sha)
        {
            try
            {
                var downloadSuccess = await DownloadMonstersJsonAsync();
                if (!downloadSuccess)
                {
                    Debug.LogError("[VersionManager] monsters.json download failed.");
                    OnDownloadFailed().Forget();
                    return false;
                }

                PlayerPrefs.SetString("localSHA", sha);

                var downloadSkinSuccess = await DownloadMonstersSkinJsonAsync();
                if (!downloadSkinSuccess)
                {
                    Debug.LogWarning(
                        "[VersionManager] pet_skin.json download failed. Reusing the existing cache or an empty structure.");
                    if (!File.Exists(SeerResources.UserDataFile(SeerResources.PetSkinFile)))
                    {
                        File.WriteAllText(
                            SeerResources.UserDataFile(SeerResources.PetSkinFile),
                            JsonConvert.SerializeObject(SeerLocalConfigExporter.CreateEmptyPetSkinsRoot(), Formatting.Indented));
                    }
                }

                if (RefreshMonstersData())
                {
                    LoadLeaderboardScene();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VersionManager] Mirror update failed.\n{ex}");
            }

            OnDownloadFailed().Forget();
            return false;
        }

        void LoadLeaderboardScene()
        {
            SceneManager.LoadSceneAsync("Leaderboard");
        }

        async UniTask OnDownloadFailed()
        {
            if (failedPanel != null)
            {
                failedPanel.SetActive(true);
            }

            await UniTask.Delay(TimeSpan.FromSeconds(3f));
            Application.Quit();
        }

        void OnGUI()
        {
            if (!_showLocalInstallPrompt || IsMobilePlatform)
            {
                return;
            }

            var width = Mathf.Min(760f, Screen.width - 40f);
            var height = 280f;
            var rect = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);

            GUILayout.Window(GetInstanceID(), rect, DrawLocalInstallPromptWindow, "Select Seer Source Root");
        }

        void DrawLocalInstallPromptWindow(int windowId)
        {
            GUILayout.Label(
                "No usable local Seer source root was detected.\n" +
                "Choose a NewSeer install root or a synced mirror root that contains Seer_Data so we can export config and assets locally.");

            GUILayout.Space(8f);
            GUILayout.Label("Source root");
            GUILayout.BeginHorizontal();
            _localInstallInput = GUILayout.TextField(_localInstallInput ?? "", GUILayout.Height(28f));
            if (GUILayout.Button("Browse...", GUILayout.Width(120f), GUILayout.Height(28f)))
            {
                BrowseLocalInstallPrompt();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            GUILayout.Label(@"Example: D:\SeerLauncher\games\NewSeer");

            if (!string.IsNullOrEmpty(_localInstallPromptError))
            {
                var oldColor = GUI.color;
                GUI.color = new Color(1f, 0.4f, 0.4f);
                GUILayout.Label(_localInstallPromptError);
                GUI.color = oldColor;
            }

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Use Source Root", GUILayout.Height(36f)))
            {
                ConfirmLocalInstallPrompt();
            }

            if (GUILayout.Button("Use Mirror Instead", GUILayout.Height(36f)))
            {
                CloseLocalInstallPrompt("");
            }
            GUILayout.EndHorizontal();
            GUI.DragWindow();
        }
    }
}
