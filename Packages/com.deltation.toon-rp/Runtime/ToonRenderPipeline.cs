﻿using DELTation.ToonRP.Extensions;
using DELTation.ToonRP.PostProcessing;
using DELTation.ToonRP.Shadows;
using DELTation.ToonRP.Xr;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace DELTation.ToonRP
{
    public sealed class ToonRenderPipeline : RenderPipeline
    {
        public const string PipelineTag = "ToonRP";

        private readonly ToonCameraRenderer _cameraRenderer = new();
        private readonly ToonRampSettings _globalRampSettings;
        private readonly ToonShadowSettings _shadowSettings;
        private ToonCameraRendererSettings _cameraRendererSettings;
        private ToonRenderingExtensionSettings _extensions;
        private ToonPostProcessingSettings _postProcessingSettings;

        public ToonRenderPipeline(in ToonCameraRendererSettings cameraRendererSettings,
            in ToonRampSettings globalRampSettings, in ToonShadowSettings shadowSettings,
            in ToonPostProcessingSettings postProcessingSettings, ToonRenderingExtensionSettings extensions)
        {
            Shader.globalRenderPipeline = PipelineTag;

            _cameraRendererSettings = cameraRendererSettings;
            _globalRampSettings = globalRampSettings;
            _shadowSettings = shadowSettings;
            _postProcessingSettings = postProcessingSettings;
            _extensions = extensions;
            GraphicsSettings.useScriptableRenderPipelineBatching = _cameraRendererSettings.UseSrpBatching;

#if ENABLE_VR && ENABLE_XR_MODULE
            var occlusionMeshShader = Shader.Find(ToonXr.OcclusionMeshShaderName);
            var mirrorViewShader = Shader.Find(ToonXr.MirrorViewShaderName);
            XRSystem.Initialize(ToonXrPass.Create, occlusionMeshShader, mirrorViewShader);
#endif // ENABLE_VR && ENABLE_XR_MODULE
        }

        public ref ToonCameraRendererSettings CameraRendererSettings => ref _cameraRendererSettings;
        public ref ToonPostProcessingSettings PostProcessingSettings => ref _postProcessingSettings;
        public ref ToonRenderingExtensionSettings Extensions => ref _extensions;

        public void InvalidateExtensions() =>
            _cameraRenderer.InvalidateExtensions();

        public static Shader GetDefaultShader() => Shader.Find("Toon RP/Default");

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _cameraRenderer.Dispose();
            XRSystem.Dispose();
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            if (QualitySettings.activeColorSpace == ColorSpace.Gamma)
            {
                Debug.LogError(
                    "Toon RP does not support Gamma color space. Please switch to Linear color space in Project Settings > Player > Other Settings > Color Space"
                );
                return;
            }

            XRSystem.SetDisplayMSAASamples((MSAASamples) _cameraRendererSettings.Msaa);

            var sharedContext = new ToonRenderPipelineSharedContext();

            foreach (Camera camera in cameras)
            {
                // Prepare XR rendering
                bool xrActive = false;
                XRLayout xrLayout = XRSystem.NewLayout();
                xrLayout.AddCamera(camera, true);

                ToonAdditionalCameraData additionalCameraData = GetOrAddAdditionalCameraData(camera);

                foreach ((Camera _, XRPass xrPass) in xrLayout.GetActivePasses())
                {
                    if (xrPass.enabled)
                    {
                        xrActive = true;
                        UpdateCameraStereoMatrices(camera, xrPass);
                    }

#if ENABLE_VR && ENABLE_XR_MODULE
                    if (xrPass.enabled)
                    {
                        additionalCameraData.XrPass = xrPass;
                        // UpdateCameraData();
                    }
#endif // ENABLE_VR && ENABLE_XR_MODULE

                    _cameraRenderer.Render(context, ref sharedContext,
                        camera, additionalCameraData,
                        _cameraRendererSettings,
                        _globalRampSettings,
                        _shadowSettings,
                        _postProcessingSettings,
                        _extensions
                    );
                }

                if (xrActive)
                {
                    CommandBuffer cmd = CommandBufferPool.Get();
                    XRSystem.RenderMirrorView(cmd, camera);
                    context.ExecuteCommandBuffer(cmd);
                    context.Submit();
                    CommandBufferPool.Release(cmd);
                }

                XRSystem.EndLayout();
            }
        }

        private static ToonAdditionalCameraData GetOrAddAdditionalCameraData(Camera camera)
        {
            if (!camera.TryGetComponent(out ToonAdditionalCameraData additionalCameraData))
            {
                additionalCameraData = camera.gameObject.AddComponent<ToonAdditionalCameraData>();
            }

            return additionalCameraData;
        }

        private static void UpdateCameraStereoMatrices(Camera camera, XRPass xr)
        {
#if ENABLE_VR && ENABLE_XR_MODULE
            if (xr.enabled)
            {
                if (xr.singlePassEnabled)
                {
                    for (int i = 0; i < Mathf.Min(2, xr.viewCount); i++)
                    {
                        camera.SetStereoProjectionMatrix((Camera.StereoscopicEye) i, xr.GetProjMatrix(i));
                        camera.SetStereoViewMatrix((Camera.StereoscopicEye) i, xr.GetViewMatrix(i));
                    }
                }
                else
                {
                    camera.SetStereoProjectionMatrix((Camera.StereoscopicEye) xr.multipassId, xr.GetProjMatrix());
                    camera.SetStereoViewMatrix((Camera.StereoscopicEye) xr.multipassId, xr.GetViewMatrix());
                }
            }
#endif // ENABLE_VR && ENABLE_XR_MODULE
        }
    }
}