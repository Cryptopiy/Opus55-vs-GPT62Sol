using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using C = LivingIsland.GameConfig;

namespace LivingIsland
{
    public enum QualityPreset { Low, Medium, High }

    /// <summary>
    /// Live quality presets (I-30): grass budget 20k / 50k / 100k, shadow distance and cascades, shadow map
    /// size, resolution scale, MSAA, water shader quality and tree LOD bias - applied without restart.
    /// In the editor the pipeline asset's original values are restored when play mode ends.
    /// </summary>
    public sealed class QualityManager : MonoBehaviour
    {
        public QualityPreset Current { get; private set; } = QualityPreset.High;

        static readonly int WaterQualityId = Shader.PropertyToID("_WaterQuality");
        UniversalRenderPipelineAsset asset;
        float origShadowDistance, origRenderScale;
        int origCascades, origShadowRes, origMsaa;
        Vector3 origSplit;
        bool saved;

        UniversalRenderPipelineAsset Asset
        {
            get
            {
                if (asset == null)
                {
                    RenderPipelineAsset rp = QualitySettings.renderPipeline;
                    if (rp == null) rp = GraphicsSettings.defaultRenderPipeline;
                    asset = rp as UniversalRenderPipelineAsset;
                    if (asset != null && !saved)
                    {
                        saved = true;
                        origShadowDistance = asset.shadowDistance; origRenderScale = asset.renderScale;
                        origCascades = asset.shadowCascadeCount; origShadowRes = asset.mainLightShadowmapResolution;
                        origMsaa = asset.msaaSampleCount; origSplit = asset.cascade4Split;
                    }
                }
                return asset;
            }
        }

        public static string Name(QualityPreset p) => p == QualityPreset.Low ? "Low" : p == QualityPreset.Medium ? "Medium" : "High";

        public void Apply(QualityPreset preset)
        {
            Current = preset;
            var a = Asset;
            var camData = Game.MainCamera != null ? Game.MainCamera.GetComponent<UniversalAdditionalCameraData>() : null;
            switch (preset)
            {
                case QualityPreset.Low:
                    Configure(a, C.GrassBudgetLow, C.ShadowDistanceLow, 2, 2048, C.RenderScaleLow, 1, 0f, C.LodBiasLow);
                    if (camData != null) camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                    break;
                case QualityPreset.Medium:
                    Configure(a, C.GrassBudgetMedium, C.ShadowDistanceMedium, 4, 2048, C.RenderScaleMedium, 2, 0.5f, C.LodBiasMedium);
                    if (camData != null) camData.antialiasing = AntialiasingMode.None;
                    break;
                default:
                    Configure(a, C.GrassBudgetHigh, C.ShadowDistanceHigh, C.ShadowCascades, 4096, C.RenderScaleHigh, 4, 1f, C.LodBiasHigh);
                    if (camData != null) camData.antialiasing = AntialiasingMode.None;
                    break;
            }
            Debug.Log("[LI] Quality preset " + Name(preset) + " applied (grass budget " + (Game.Grass != null ? Game.Grass.Budget : 0) + ")");
        }

        void Configure(UniversalRenderPipelineAsset a, int grassBudget, float shadowDistance, int cascades, int shadowRes,
                       float renderScale, int msaa, float waterQuality, float lodBias)
        {
            if (Game.Grass != null) Game.Grass.SetBudget(grassBudget);
            if (a != null)
            {
                a.shadowDistance = shadowDistance;
                a.shadowCascadeCount = cascades;
                // Cascade ends at ~8 m, ~24 m, ~56 m and the full distance (fine near the player).
                a.cascade4Split = new Vector3(8f / shadowDistance, 24f / shadowDistance, 56f / shadowDistance);
                a.cascade2Split = 15f / shadowDistance;
                a.mainLightShadowmapResolution = shadowRes;
                a.renderScale = renderScale;
                a.msaaSampleCount = msaa;
            }
            Shader.SetGlobalFloat(WaterQualityId, waterQuality);
            QualitySettings.lodBias = lodBias;
        }

        void OnDestroy()
        {
            // Runtime edits of the pipeline asset would otherwise persist in the project (editor only).
            if (asset != null && saved)
            {
                asset.shadowDistance = origShadowDistance; asset.renderScale = origRenderScale;
                asset.shadowCascadeCount = origCascades; asset.mainLightShadowmapResolution = origShadowRes;
                asset.msaaSampleCount = origMsaa; asset.cascade4Split = origSplit;
            }
        }
    }
}
