using System;
using System.IO;
using GoldenBoot.BlockDodge;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// Sets up the project (scene, icon, splash, player settings) from code and builds the Android APK.
// CI calls BuildScript.BuildAndroid through GameCI's buildMethod input.
public static class BuildScript
{
    const string ScenePath = "Assets/Scenes/Main.unity";
    const string IconPath = "Assets/Art/Icon.png";
    const string LogoPath = "Assets/Art/SplashLogo.png";
    const string AppId = "com.goldenboot.blockdodge";

    [MenuItem("Block Dodge/Build Android APK")]
    public static void BuildAndroid()
    {
        Setup();

        string path = Arg("-customBuildPath");
        if (string.IsNullOrEmpty(path)) path = "Builds/Android/BlockDodge.apk";
        string dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        EditorUserBuildSettings.buildAppBundle = path.EndsWith(".aab", StringComparison.OrdinalIgnoreCase);

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = path,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None
        });

        Debug.Log("Build result: " + report.summary.result + ", size " + report.summary.totalSize +
                  " bytes, " + report.summary.totalErrors + " errors, output " + path);
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("Build failed: " + report.summary.result);
    }

    [MenuItem("Block Dodge/Apply project setup")]
    public static void Setup()
    {
        EnsureScene();
        Texture2D icon = WriteTexture(IconPath, Art.Icon(512, true), false);
        WriteTexture(LogoPath, Art.Icon(512, false), true);
        Sprite logo = AssetDatabase.LoadAssetAtPath<Sprite>(LogoPath);
        ConfigurePlayer(icon, logo);
        AssetDatabase.SaveAssets();
    }

    static void EnsureScene()
    {
        if (!File.Exists(ScenePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
    }

    static Texture2D WriteTexture(string path, Texture2D tex, bool sprite)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
        if (sprite) importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 1024;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void ConfigurePlayer(Texture2D icon, Sprite logo)
    {
        NamedBuildTarget android = NamedBuildTarget.Android;

        PlayerSettings.companyName = "GoldenBoot";
        PlayerSettings.productName = "Block Dodge";
        PlayerSettings.bundleVersion = "1.0";
        int code;
        PlayerSettings.Android.bundleVersionCode =
            int.TryParse(Arg("-androidVersionCode"), out code) && code > 0 ? code : 1;

        PlayerSettings.SetApplicationIdentifier(android, AppId);
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

        if (icon != null)
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

        PlayerSettings.SplashScreen.show = true;
        PlayerSettings.SplashScreen.showUnityLogo = false;
        PlayerSettings.SplashScreen.backgroundColor = Art.Background;
        PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Dolly;
        PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.AllSequential;
        if (logo != null)
            PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(1.4f, logo) };
    }

    static string Arg(string name)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }
}
