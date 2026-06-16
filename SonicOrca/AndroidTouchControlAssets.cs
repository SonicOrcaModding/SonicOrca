using Hjg.Pngcs;
using SonicOrca.Graphics;
using SonicOrca.Resources;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace SonicOrca;

public static class AndroidTouchControlAssets
{
#if __ANDROID__ || __IOS__
    private static bool _loaded;
    private static bool _available;
    private static bool _started;
    private static Task<RawPixels[]> _decodeTask;

    public const string KEY_DPAD_BOTTOM      = "SONICORCA/MOBILE/DPAD_BOTTOM";
    public const string KEY_DPAD_TOP         = "SONICORCA/MOBILE/DPAD_TOP";
    public const string KEY_BUTTON_MAIN      = "SONICORCA/MOBILE/BUTTON_MAIN";
    public const string KEY_BUTTON_MAIN_ALT  = "SONICORCA/MOBILE/BUTTON_MAIN_ALT";
    public const string KEY_BUTTON_BACK      = "SONICORCA/MOBILE/BUTTON_MAIN";

    public static bool Available => _available;

    public static ITexture DpadBottom    { get; private set; }
    public static ITexture DpadTop       { get; private set; }
    public static ITexture ButtonMain    { get; private set; }
    public static ITexture ButtonMainAlt { get; private set; }
    public static ITexture ButtonBack    { get; private set; }

    private const string LogTag = "S2HD";

    private static void Log(string message)
    {
#if __ANDROID__
        try { global::Android.Util.Log.Info(LogTag, message); } catch { }
#endif
        try { Trace.WriteLine(message); } catch { }
    }

    private static string ResolveKey(ResourceTree tree, string desiredKey)
    {
        foreach (string candidate in new[] { desiredKey, desiredKey + ".png", desiredKey + ".PNG" })
        {
            if (tree[candidate] != null)
                return candidate;
        }

        var listing = tree.GetNodeListing();
        foreach (string key in listing.Keys)
        {
            if (string.Equals(key, desiredKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, desiredKey + ".png", StringComparison.OrdinalIgnoreCase))
                return key;
        }
        foreach (string key in listing.Keys)
        {
            if (key != null && (key.EndsWith(desiredKey, StringComparison.OrdinalIgnoreCase) ||
                                key.EndsWith(desiredKey + ".png", StringComparison.OrdinalIgnoreCase)))
                return key;
        }
        return null;
    }

    public static void EnsureLoaded(SonicOrcaGameContext gameContext) { }

    public static void Preload(SonicOrcaGameContext gameContext)
    {
        if (_loaded)
            return;
        if (gameContext == null)
            throw new ArgumentNullException(nameof(gameContext));

        ResourceTree tree = gameContext.ResourceTree;

        if (!_started)
        {
            string kDpadBottom    = ResolveKey(tree, KEY_DPAD_BOTTOM);
            string kDpadTop       = ResolveKey(tree, KEY_DPAD_TOP);
            string kButtonMain    = ResolveKey(tree, KEY_BUTTON_MAIN);
            string kButtonMainAlt = ResolveKey(tree, KEY_BUTTON_MAIN_ALT);
            string kButtonBack    = ResolveKey(tree, KEY_BUTTON_BACK);

            Log($"[AndroidTouchControlAssets] Scheduling decode: " +
                $"bottom={kDpadBottom ?? "null"}, top={kDpadTop ?? "null"}, " +
                $"main={kButtonMain ?? "null"}, mainAlt={kButtonMainAlt ?? "null"}, back={kButtonBack ?? "null"}");

            if (kDpadBottom == null || kDpadTop == null || kButtonMain == null)
            {
                Log("[AndroidTouchControlAssets] Required keys missing — touch controls unavailable.");
                _available = false;
                _loaded = true;
                return;
            }

            _started = true;

            _decodeTask = Task.Run(() =>
                DecodeAll(tree, kDpadBottom, kDpadTop, kButtonMain, kButtonMainAlt, kButtonBack));

            return;
        }

        if (_decodeTask == null || !_decodeTask.IsCompleted)
            return;

        if (_decodeTask.IsFaulted || _decodeTask.IsCanceled)
        {
            Log($"[AndroidTouchControlAssets] Decode failed: " +
                $"{_decodeTask.Exception?.GetBaseException()?.Message}");
            _available = false;
            _loaded = true;
            return;
        }

        try
        {
            RawPixels[] px  = _decodeTask.Result;
            var ctx         = gameContext.Window.GraphicsContext;

            DpadBottom    = ctx.CreateTexture(px[0].Width, px[0].Height, px[0].Channels, px[0].Data);
            DpadTop       = ctx.CreateTexture(px[1].Width, px[1].Height, px[1].Channels, px[1].Data);
            ButtonMain    = ctx.CreateTexture(px[2].Width, px[2].Height, px[2].Channels, px[2].Data);
            ButtonMainAlt = px[3].Data != null
                ? ctx.CreateTexture(px[3].Width, px[3].Height, px[3].Channels, px[3].Data)
                : ButtonMain;
            ButtonBack    = px[4].Data != null
                ? ctx.CreateTexture(px[4].Width, px[4].Height, px[4].Channels, px[4].Data)
                : ButtonMain;

            _available = DpadBottom != null && DpadTop != null && ButtonMain != null;
            Log($"[AndroidTouchControlAssets] Upload done. available={_available}");
        }
        catch (Exception ex)
        {
            Log($"[AndroidTouchControlAssets] Upload failed: {ex.GetType().Name} {ex.Message}");
            _available = false;
        }

        _loaded = true;
    }

    private struct RawPixels
    {
        public int    Width, Height, Channels;
        public byte[] Data;
    }

    private static RawPixels[] DecodeAll(
        ResourceTree tree,
        string kBottom, string kTop, string kMain, string kMainAlt, string kBack)
    {
        return new[]
        {
            DecodePng(tree, kBottom),
            DecodePng(tree, kTop),
            DecodePng(tree, kMain),
            kMainAlt != null ? DecodePng(tree, kMainAlt) : default,
            kBack    != null ? DecodePng(tree, kBack)    : default,
        };
    }

    private static RawPixels DecodePng(ResourceTree tree, string key)
    {
        Resource res = tree[key]?.Resource
            ?? throw new InvalidOperationException($"Resource not found in tree: {key}");

        using Stream stream = res.Source.ReadUncompressed();

        var reader   = new PngReader(stream);
        int width    = reader.ImgInfo.Cols;
        int height   = reader.ImgInfo.Rows;
        int channels = reader.ImgInfo.Channels;
        byte[] data  = new byte[width * height * channels];

        for (int row = 0; row < height; row++)
        {
            ImageLine line = reader.ReadRowByte(row);
            Buffer.BlockCopy(
                line.ScanlineB, 0,
                data, row * width * channels,
                width * channels);
        }
        reader.End();

        return new RawPixels { Width = width, Height = height, Channels = channels, Data = data };
    }

#else
    public static bool Available => false;
    public static ITexture DpadBottom => null;
    public static ITexture DpadTop => null;
    public static ITexture ButtonMain => null;
    public static ITexture ButtonMainAlt => null;
    public static ITexture ButtonBack => null;
    public static void EnsureLoaded(SonicOrcaGameContext gameContext) { }
    public static void Preload(SonicOrcaGameContext gameContext) { }
#endif
}