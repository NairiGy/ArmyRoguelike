using UnityEditor;
using UnityEngine;

/// <summary>
/// Import settings for every clip under Assets/Audio, so nobody sets them by hand: mono, Vorbis.
/// Short sounds are decompressed on load (no delay when they play); loops (names ending in "_loop")
/// stay compressed in memory.
/// </summary>
public sealed class SfxImport : AssetPostprocessor
{
    private void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/Audio/"))
            return;

        var importer = (AudioImporter)assetImporter;
        importer.forceToMono = true;
        importer.loadInBackground = false;

        var loop = System.IO.Path.GetFileNameWithoutExtension(assetPath).EndsWith("_loop");
        var settings = importer.defaultSampleSettings;
        settings.loadType = loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        settings.quality = 0.7f;
        settings.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
        importer.defaultSampleSettings = settings;
    }
}
