using System;
using DELTation.ToonRP.Editor.ShaderGUI;
using DELTation.ToonRP.Editor.ShaderGUI.ShaderGraph;
using UnityEditor;
using UnityEditor.ShaderGraph;
using UnityEngine;
using UnityEngine.UIElements;
using static DELTation.ToonRP.Editor.ToonShaderUtils;

namespace DELTation.ToonRP.Editor.ShaderGraph.Targets
{
    internal sealed class ToonParticlesUnlitSubTarget : ToonSubTarget
    {
        private static readonly GUID
            SourceCodeGuid = new("c4a8f49a216a4c2f8c8fd14b70dbbb2c"); // ToonParticlesSubTarget.cs

        public ToonParticlesUnlitSubTarget() => displayName = "Particles (Unlit)";

        protected override ShaderID ShaderID => ShaderID.ParticlesUnlit;

        // ReSharper disable Unity.RedundantSerializeFieldAttribute
        [field: SerializeField]
        private bool SoftParticles { get; set; }
        // ReSharper restore Unity.RedundantSerializeFieldAttribute

        private bool SoftParticlesEffectivelyEnabled => SoftParticles && SoftParticlesCanBeEnabled;

        private bool SoftParticlesCanBeEnabled => target.SurfaceType == SurfaceType.Transparent;


        public override bool IsActive() => true;

        public override void Setup(ref TargetSetupContext context)
        {
            context.AddAssetDependency(SourceCodeGuid, AssetCollection.Flags.SourceDependency);
            base.Setup(ref context);

            Type toonRpType = typeof(ToonRenderPipelineAsset);
            if (!context.HasCustomEditorForRenderPipeline(toonRpType))
            {
                Type gui = typeof(ToonRpShaderGraphParticlesUnlitShaderGui);
                context.AddCustomEditorForRenderPipeline(gui.FullName, toonRpType);
            }

            // Process SubShaders
            context.AddSubShader(PostProcessSubShader(SubShaders.ParticlesUnlit(target, target.RenderType,
                        target.RenderQueueString, SoftParticlesEffectivelyEnabled
                    )
                )
            );
        }

        public override void GetActiveBlocks(ref TargetActiveBlockContext context)
        {
            base.GetActiveBlocks(ref context);

            context.AddBlock(ToonBlockFields.SurfaceDescription.SoftParticlesDistance, SoftParticlesEffectivelyEnabled);
            context.AddBlock(ToonBlockFields.SurfaceDescription.SoftParticlesRange, SoftParticlesEffectivelyEnabled);
        }

        public override void GetPropertiesGUI(ref TargetPropertyGUIContext context, Action onChange,
            Action<string> registerUndo)
        {
            base.GetPropertiesGUI(ref context, onChange, registerUndo);

            if (SoftParticlesCanBeEnabled)
            {
                context.AddProperty("Soft Particles", new Toggle { value = SoftParticles }, evt =>
                    {
                        if (Equals(SoftParticles, evt.newValue))
                        {
                            return;
                        }

                        registerUndo("Change Soft Particles");
                        SoftParticles = evt.newValue;
                        onChange();
                    }
                );
            }
        }

        #region SubShader

        private static class SubShaders
        {
            public static SubShaderDescriptor ParticlesUnlit(ToonTarget target, string renderType, string renderQueue,
                bool softParticles)
            {
                const string customTags = "\"PreviewType\"=\"Plane\"";

                var result = new SubShaderDescriptor
                {
                    pipelineTag = ToonRenderPipeline.PipelineTag,
                    renderType = renderType,
                    renderQueue = renderQueue,
                    generatesPreview = true,
                    passes = new PassCollection
                    {
                        ParticlesUnlitPasses.Forward(target, softParticles),
                    },
                    customTags = customTags,
                };

                CorePasses.AddPrePasses(target, ref result);
                CorePasses.AddShadowCasterPass(target, ref result);

                return result;
            }
        }

        #endregion

        #region Pass

        private static class ParticlesUnlitPasses
        {
            private static void AddSoftParticlesControlToPass(ref PassDescriptor pass, ToonTarget target,
                bool softParticles)
            {
                if (target.AllowMaterialOverride)
                {
                    pass.keywords.Add(ParticlesUnlitKeywords.SoftParticles);
                }
                else if (softParticles)
                {
                    pass.defines.Add(ParticlesUnlitKeywords.SoftParticles, 1);
                }
            }

            public static PassDescriptor Forward(ToonTarget target, bool softParticles)
            {
                ref readonly ToonPasses.Pass pass = ref ToonPasses.ForwardParticlesUnlit;
                var result = new PassDescriptor
                {
                    // Definition
                    displayName = pass.Name,
                    referenceName = pass.ReferenceName,
                    useInPreview = true,

                    // Template
                    passTemplatePath = ToonTarget.UberTemplatePath,
                    sharedTemplateDirectories = ToonTarget.SharedTemplateDirectories,

                    // Port Mask
                    validVertexBlocks = ParticlesUnlitBlockMasks.VertexForward,
                    validPixelBlocks = ParticlesUnlitBlockMasks.ParticlesUnlitForward,

                    // Fields
                    structs = CoreStructCollections.Default,
                    requiredFields = UnlitRequiredFields.Unlit,
                    fieldDependencies = CoreFieldDependencies.Default,

                    // Conditional State
                    renderStates = CoreRenderStates.UberSwitchedRenderState(target),
                    pragmas = CorePragmas.Forward,
                    defines = new DefineCollection(),
                    keywords = new KeywordCollection(),
                    includes = ParticlesUnlitIncludes.Unlit,

                    // Custom Interpolator Support
                    customInterpolators = CoreCustomInterpDescriptors.Common,
                };

                CorePasses.AddTargetSurfaceControlsToPass(ref result, target);
                AddSoftParticlesControlToPass(ref result, target, softParticles);

                return result;
            }

            #region PortMasks

            private static class ParticlesUnlitBlockMasks
            {
                public static readonly BlockFieldDescriptor[] VertexForward =
                {
                    ToonBlockFields.VertexDescription.Position,
                    ToonBlockFields.VertexDescription.Normal,
                    ToonBlockFields.VertexDescription.Tangent,
                };

                public static readonly BlockFieldDescriptor[] ParticlesUnlitForward =
                {
                    ToonBlockFields.SurfaceDescription.Albedo,
                    ToonBlockFields.SurfaceDescription.Emission,
                    ToonBlockFields.SurfaceDescription.Alpha,
                    ToonBlockFields.SurfaceDescription.AlphaClipThreshold,

                    ToonBlockFields.SurfaceDescription.SoftParticlesDistance,
                    ToonBlockFields.SurfaceDescription.SoftParticlesRange,
                };
            }

            #endregion

            #region RequiredFields

            private static class UnlitRequiredFields
            {
                public static readonly FieldCollection Unlit = new()
                {
                    StructFields.Varyings.positionWS,
                    StructFields.Varyings.normalWS,
                    ToonStructFields.Varyings.fogFactorAndVertexLight,
                };
            }

            #endregion
        }

        #endregion

        #region Keywords

        private static class ParticlesUnlitKeywords
        {
            public static readonly KeywordDescriptor SoftParticles = new()
            {
                displayName = ShaderKeywords.SoftParticles,
                referenceName = ShaderKeywords.SoftParticles,
                type = KeywordType.Boolean,
                definition = KeywordDefinition.ShaderFeature,
                scope = KeywordScope.Local,
                stages = KeywordShaderStage.Fragment,
            };
        }

        #endregion


        #region Includes

        private static class ParticlesUnlitIncludes
        {
            private const string ParticlesUnlitPass =
                "Packages/com.deltation.toon-rp/Editor/ShaderGraph/Includes/ParticlesUnlitPass.hlsl";

            public static readonly IncludeCollection Unlit = new()
            {
                // Pre-graph
                CoreIncludes.CorePregraph,
                CoreIncludes.ShaderGraphPregraph,

                // Post-graph
                CoreIncludes.CorePostgraph,
                { ParticlesUnlitPass, IncludeLocation.Postgraph },
            };
        }

        #endregion
    }
}