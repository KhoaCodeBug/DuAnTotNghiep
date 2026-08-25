using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class VietnameseFontRuntimeTests
{
    [UnityTest]
    public IEnumerator HostAndClientFontProbesDoNotMutateStaticAtlasAfterLegacyRefreshWindow()
    {
        AsyncOperation loadMenu = SceneManager.LoadSceneAsync(0);
        while (!loadMenu.isDone) yield return null;

        TMP_FontAsset staticFont = Resources.Load<TMP_FontAsset>("Fonts/Vietnamese Static SDF");
        TMP_FontAsset liberation = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        Assert.That(staticFont, Is.Not.Null);
        Assert.That(liberation, Is.Not.Null);
        Assert.That(staticFont.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));

        Type localizationType = Type.GetType("GameLocalization, Assembly-CSharp");
        Type driverType = Type.GetType("RuntimeLocalizationDriver, Assembly-CSharp");
        Assert.That(localizationType, Is.Not.Null);
        Assert.That(driverType, Is.Not.Null);
        Assert.That(driverType.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic), Is.Null,
            "The localization driver must not restore the old 0.4-second polling mutation.");

        MethodInfo getRuntimeFont = localizationType.GetMethod("GetRuntimeFont",
            BindingFlags.Public | BindingFlags.Static);
        Assert.That(getRuntimeFont, Is.Not.Null);
        TMP_FontAsset[] fallbacksBefore = liberation.fallbackFontAssetTable.ToArray();
        object resolvedPreferred = getRuntimeFont.Invoke(null, new object[] { liberation });
        Assert.That(resolvedPreferred, Is.SameAs(liberation));
        Assert.That(liberation.fallbackFontAssetTable, Is.EqualTo(fallbacksBefore),
            "Resolving a preferred font must not mutate its serialized fallback table.");

        int characterCountBefore = staticFont.characterTable.Count;
        GameObject hostObject = new GameObject("Host Vietnamese Font Probe", typeof(RectTransform), typeof(TextMeshProUGUI));
        GameObject clientObject = new GameObject("Client Vietnamese Font Probe", typeof(RectTransform), typeof(TextMeshProUGUI));
        TextMeshProUGUI hostLabel = hostObject.GetComponent<TextMeshProUGUI>();
        TextMeshProUGUI clientLabel = clientObject.GetComponent<TextMeshProUGUI>();
        hostLabel.font = staticFont;
        clientLabel.font = staticFont;
        hostLabel.text = "HOST • TIẾNG VIỆT: ĐẦY ĐỦ DẤU … ■ □";
        clientLabel.text = "CLIENT • Trạm Radio phụ trợ: sẵn sàng … ■ □";
        hostLabel.ForceMeshUpdate(true, true);
        clientLabel.ForceMeshUpdate(true, true);

        yield return new WaitForSecondsRealtime(0.65f);

        Assert.That(hostLabel.font, Is.SameAs(staticFont));
        Assert.That(clientLabel.font, Is.SameAs(staticFont));
        Assert.That(staticFont.characterTable.Count, Is.EqualTo(characterCountBefore),
            "Host/client rendering must not add glyphs to the baked atlas at runtime.");

        UnityEngine.Object.Destroy(hostObject);
        UnityEngine.Object.Destroy(clientObject);
        yield return null;
    }
}
