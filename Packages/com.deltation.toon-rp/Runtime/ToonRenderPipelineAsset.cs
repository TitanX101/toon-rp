﻿using System.Linq;
using DELTation.ToonRP.Attributes;
using DELTation.ToonRP.Extensions;
using DELTation.ToonRP.Lighting;
using DELTation.ToonRP.PostProcessing;
using DELTation.ToonRP.Shadows;
using DELTation.ToonRP.Shadows.Blobs;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif // UNITY_EDITOR

namespace DELTation.ToonRP
{
    [CreateAssetMenu(menuName = "Toon RP/Toon Render Pipeline Asset")]
    public sealed class ToonRenderPipelineAsset : RenderPipelineAsset
    {
        private static readonly string[] ForceIncludedShaderNames =
        {
            ToonBlitter.DefaultBlitShaderPath,
            ToonVsmShadows.BlurShaderName,
            ToonBlobShadows.ShaderName,
        };

        // Hold references to all shaders access in runtime to ensure they get included to the build
        [HideInInspector]
        public Shader[] ForceIncludedShaders;

        public ToonRampSettings GlobalRampSettings = new()
        {
            Threshold = 0.0f,
            SpecularThreshold = 0.99f,
            RimThreshold = 0.5f,
            Smoothness = 0.02f,
            SpecularSmoothness = 0.005f,
            RimSmoothness = 0.1f,
            AdditionalLights = new ToonRampSettings.AdditionalLightsRamp
            {
                DiffuseOffset = 0.0f,
                SpecularOffset = 0.0f,
                DistanceAttenuationFactor = 1.0f,
            },
        };

        public ToonCameraRendererSettings CameraRendererSettings = new()
        {
            RenderTextureFormat = GraphicsFormat.R8G8B8A8_SRGB,
            Msaa = ToonCameraRendererSettings.MsaaMode.Off,
            RenderScale = 1.0f,
            MaxRenderTextureHeight = 0,
            MaxRenderTextureWidth = 0,
            RenderTextureFilterMode = FilterMode.Bilinear,
            OpaqueLayerMask = -1,
            TransparentLayerMask = -1,
            UseSrpBatching = true,
            UseDynamicBatching = false,
            NativeRenderPasses = false,
        };

        public ToonShadowSettings ShadowSettings = new()
        {
            Mode = ToonShadowSettings.ShadowMode.Vsm,
            Threshold = 0.5f, Smoothness = 0.075f,
            MaxDistance = 100.0f,
            DistanceFade = 0.1f,
            PatternScale = new Vector3(1, 0, 1),
            Vsm = new ToonVsmShadowSettings
            {
                DepthBits = ToonVsmShadowSettings.ShadowMapBits._16,
                Blur = ToonVsmShadowSettings.BlurMode.Box,
                BlurEarlyBail = true,
                BlurEarlyBailThreshold = 0.01f,
                LightBleedingReduction = 0.4f,
                Directional =
                {
                    Enabled = true, AtlasSize = TextureSize._1024,
                    CascadeCount = 4,
                    CascadeRatio1 = 0.1f,
                    CascadeRatio2 = 0.25f,
                    CascadeRatio3 = 0.5f,
                },
                SoftShadows =
                {
                    Spread = 0.8f,
                },
            },
            Blobs = new ToonBlobShadowsSettings
            {
                Saturation = 1.0f,
                AtlasSize = TextureSize._128,
                Mode = BlobShadowsMode.Default,
            },
        };

        [ToonRpHeader]
        public ToonRenderingExtensionSettings Extensions;

        [ToonRpHeader("Post-Processing")]
        public ToonPostProcessingSettings PostProcessing;

        [CanBeNull]
        private Material _defaultMaterial;

        public override Material defaultMaterial
        {
            get
            {
                if (_defaultMaterial != null)
                {
                    return _defaultMaterial;
                }

#if UNITY_EDITOR
                _defaultMaterial =
                    AssetDatabase.LoadAssetAtPath<Material>("Packages/com.deltation.toon-rp/Assets/Toon RP Default.mat"
                    );
#endif // UNITY_EDITOR
                return _defaultMaterial;
            }
        }

        public override Shader defaultShader => ToonRenderPipeline.GetDefaultShader();

#if UNITY_2022_3_OR_NEWER
        public override string renderPipelineShaderTag => ToonRenderPipeline.PipelineTag;
#endif // UNITY_2022_3_OR_NEWER

        private void Reset()
        {
            EnsureRequiredValuesArePresent();
        }

        protected override void OnValidate()
        {
            base.OnValidate();

            if (ForceIncludedShaders == null || ForceIncludedShaders.Length != ForceIncludedShaderNames.Length)
            {
                ForceIncludedShaders = ForceIncludedShaderNames.Select(Shader.Find).ToArray();
            }

            EnsureRequiredValuesArePresent();

            if (ShadowSettings.Vsm.LightBleedingReduction == 0.0f)
            {
                ShadowSettings.Vsm.LightBleedingReduction = 0.4f;
            }

            if (ShadowSettings.Vsm.DepthBits == 0)
            {
                ShadowSettings.Vsm.DepthBits = ToonVsmShadowSettings.ShadowMapBits._16;
            }

            if (ShadowSettings.Vsm.BlurScatter < 1.0f)
            {
                ShadowSettings.Vsm.BlurScatter = 1.0f;
            }

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (CameraRendererSettings.MaxLightsPerTile == 0)
            {
                CameraRendererSettings.MaxLightsPerTile = ToonTiledLighting.MaxLightsPerTile / 2;
            }

            CameraRendererSettings.PrePass = CameraRendererSettings.PrePass.Sanitize();
        }

        private void EnsureRequiredValuesArePresent()
        {
#if UNITY_EDITOR
            ref Texture3D texture = ref ShadowSettings.Vsm.SoftShadows.RotatedPoissonSamplingTexture;
            if (texture == null)
            {
                ShadowSettings.Vsm.SoftShadows.RotatedPoissonSamplingTexture =
                    AssetDatabase.LoadAssetAtPath<Texture3D>(
                        "Packages/com.deltation.toon-rp/Assets/DefaultRotatedPoissonSamplingTexture.asset"
                    );
            }
#endif // UNITY_EDITOR
        }

        public PrePassMode GetEffectiveDepthPrePassMode() =>
            ToonCameraRenderer.GetOverridePrePassMode(CameraRendererSettings, PostProcessing, Extensions);

        protected override RenderPipeline CreatePipeline() =>
            new ToonRenderPipeline(CameraRendererSettings, GlobalRampSettings, ShadowSettings, PostProcessing,
                Extensions
            );
    }
}