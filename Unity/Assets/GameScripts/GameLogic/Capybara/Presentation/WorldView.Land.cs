using System;
using UnityEngine;
using UnityEngine.UI;

namespace CapybaraRebuild
{
    internal sealed partial class WorldView
    {
        [Serializable]
        private sealed class LandSceneryCatalog
        {
            public LandScenery[] maps = Array.Empty<LandScenery>();
        }

        [Serializable]
        private sealed class LandScenery
        {
            public int id = 0;
            public float playerY = 0, skyY = 0, skyPivot = 0, farY = 0, middleY = 0;
            public float groundY = 0, secondGroundY = 0, cloudY = 0, cloudPivot = .5f;
            public int[] secondGround = Array.Empty<int>();
            public float ridgeY = 0, roadsideY = 0, foregroundY = 0;
            public bool snowDecorations = false;
        }

        private const float MapTileUnits = 10.8f;
        private static LandSceneryCatalog landCatalog;
        private LandScenery landScenery;
        private readonly RawImage[] secondGround;
        private readonly RawImage snowCloud, snowRidge, snowRoadside, snowForeground;

        private void SetLandScenery()
        {
            if (landCatalog == null)
            {
                var data = Resources.Load<TextAsset>("LandScenery");
                landCatalog = data != null ? JsonUtility.FromJson<LandSceneryCatalog>(data.text) : new LandSceneryCatalog();
            }

            landScenery = !campView && map?.hasArt == true && landCatalog.maps != null
                ? Array.Find(landCatalog.maps, profile => profile.id == map.id)
                : null;
            foreach (var tile in secondGround) tile.enabled = false;

            bool snow = landScenery?.snowDecorations == true;
            snowCloud.enabled = snow && snowCloud.texture != null;
            snowRidge.enabled = snow && snowRidge.texture != null;
            snowRoadside.enabled = snow && snowRoadside.texture != null;
            snowForeground.enabled = snow && snowForeground.texture != null;
        }

        private void LayoutLandScenery(float height, float width)
        {
            if (height <= 0 || width <= 0) return;

            // The terrain sprites use 100 pixels per unit with separate pivots per layer.
            var profile = landScenery;
            float scale = ChapterWorldScale(new Vector2(width, height));
            float origin = TrailFootY * height - profile.playerY * scale;
            float skyHeight = TextureHeight(map.sky) * scale;
            float skyBottom = origin + profile.skyY * scale - profile.skyPivot * skyHeight;
            sky.rectTransform.anchorMin = new Vector2(0, skyBottom / height);
            sky.rectTransform.anchorMax = new Vector2(1, Mathf.Max(1, (skyBottom + skyHeight) / height));

            float position = travel * width;
            float sceneryWidth = Mathf.Max(width, MapTileUnits * scale);
            float farPosition = position * Mathf.Abs(map.farSpeed) * .5f;
            float farHeight = TextureHeight(map.far) * scale;
            ScrollLayer(far, map.far, farPosition, origin + profile.farY * scale + farHeight * .5f, sceneryWidth, farHeight);
            ScrollLayer(middle, map.middle, position, origin + profile.middleY * scale, sceneryWidth, TextureHeight(map.middle) * scale);

            const float groundWidthRatio = 906f / 1080f;
            float groundWidth = sceneryWidth * groundWidthRatio;
            float groundHeight = TextureHeight(map.ground) * scale;
            ScrollLayer(ground, map.ground, position, origin + profile.groundY * scale + groundHeight * .5f, groundWidth, groundHeight);
            float upperHeight = TextureHeight(profile.secondGround) * scale;
            ScrollLayer(secondGround, profile.secondGround, position,
                origin + profile.secondGroundY * scale + upperHeight * .5f, groundWidth, upperHeight);

            if (profile.snowDecorations)
            {
                LayoutSnowStrip(snowCloud, width, height, scale, origin + profile.cloudY * scale,
                    MapTileUnits * 1.6f, position * .12f + time * Mathf.Abs(map.cloudSpeed) * scale);
                LayoutSnowStrip(snowRidge, width, height, scale, origin + profile.ridgeY * scale,
                    MapTileUnits * 1.8f, farPosition);
                LayoutSnowStrip(snowRoadside, width, height, scale, origin + profile.roadsideY * scale,
                    MapTileUnits * 1.65f, position);
                LayoutSnowStrip(snowForeground, width, height, scale, origin + profile.foregroundY * scale,
                    MapTileUnits * 1.45f, position * 1.08f);
            }
            else
                LayoutCloudTiles(width, false, origin + profile.cloudY * scale - height, 1 - profile.cloudPivot);
        }

        private float TextureHeight(int[] ids) =>
            ids is { Length: > 0 } && MapTexture(ids[0]) is Texture2D texture ? texture.height / 100f : 0;

        private static void LayoutSnowStrip(RawImage layer, float width, float height, float scale,
            float baseline, float repeatUnits, float position)
        {
            if (!layer.enabled) return;
            float repeatWidth = repeatUnits * scale;
            float stripHeight = layer.texture.height * repeatWidth / layer.texture.width;
            var rect = layer.rectTransform;
            rect.sizeDelta = new Vector2(width, stripHeight);
            rect.anchoredPosition = new Vector2(0, baseline + stripHeight - height);
            layer.uvRect = new Rect(Mathf.Repeat(position / repeatWidth, 1), 0, width / repeatWidth, 1);
        }
    }
}
