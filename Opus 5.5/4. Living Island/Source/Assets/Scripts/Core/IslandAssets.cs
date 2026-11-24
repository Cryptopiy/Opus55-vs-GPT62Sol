using UnityEngine;

namespace LivingIsland
{
    /// <summary>
    /// References to the project's shaders and the Blender props imported from Assets/Models/*.glb.
    /// Lives at Assets/Resources/IslandAssets.asset (created by the editor setup script) so the shaders
    /// and models are included in builds and can be loaded without any manual scene setup.
    /// </summary>
    public sealed class IslandAssets : ScriptableObject
    {
        [Header("Shaders")]
        public Shader terrainShader;
        public Shader waterShader;
        public Shader skyShader;
        public Shader skyCubeShader;
        public Shader grassShader;
        public Shader barkShader;
        public Shader foliageShader;
        public Shader rockShader;
        public Shader propShader;
        public Shader glassShader;
        public Shader flowerShader;
        public Shader particleAdditiveShader;
        public Shader particleAlphaShader;
        public Shader rainShader;
        public Shader beamShader;
        public Shader lightningShader;
        public ComputeShader grassCompute;

        [Header("Blender props (Assets/Models)")]
        public GameObject[] palms = new GameObject[3];
        public GameObject[] rocks = new GameObject[5];
        public GameObject lighthouse;
        public GameObject rowboat;
        public GameObject driftwood;
        public GameObject campfireLogs;

        public static IslandAssets Load()
        {
            var assets = Resources.Load<IslandAssets>("IslandAssets");
            if (assets == null) Debug.LogError("[LI] Resources/IslandAssets.asset is missing - run LivingIsland.EditorTools.ProjectSetup.Run");
            return assets;
        }
    }
}
