using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[Serializable]
class VolumetricLighting2DSettings
{
    public VolumetricLighting2DSettings() {}
    public VolumetricLighting2DSettings(VolumetricLighting2DSettings other)
    {
        _ResolutionScale = other.ResolutionScale;
        _Intensity = other.Intensity;
        _Intensity = other.BlurWidth;
        _RenderPassEvent = other.RenderPassEvent;
        _ProfilerTag = other.ProfilerTag;
    }
    [SerializeField]
    private Texture2D _LightTexture = null;
    public Texture2D LightTexture
    {
        get => _LightTexture;
        set => _LightTexture = value;
    }
    [Range(-1.0f, 1.0f)]
    [SerializeField]
    private float _LightScreenPositionX = 0;
    [Range(-1.0f, 1.0f)]
    [SerializeField]
    private float _LightScreenPositionY = 0;
    //[SerializeField]
    //private Vector2 _LightScreenPosition = Vector2.zero;
    public Vector2 LightScreenPosition
    {
        get => new Vector2(_LightScreenPositionX, _LightScreenPositionY);
        set
        {
            _LightScreenPositionX = value.x;
            _LightScreenPositionY = value.y;
        }
    }
    [Range(0.1f, 1.0f)]
    [SerializeField]
    private float _ResolutionScale = 0.5f;
    public float ResolutionScale
    {
        get => _ResolutionScale;
        set => _ResolutionScale = Mathf.Clamp(value, 0.1f, 1f);
    }
    [Range(0, 256)]
    [SerializeField]
    private int _Samples = 128;
    public int Samples
    {
        get => _Samples;
        set => _Samples = Mathf.Clamp(value, 0, 256);
    }
    [Range(0.0f, 1.0f)]
    [SerializeField]
    private float _Intensity = 1.0f;
    public float Intensity
    {
        get => _Intensity;
        set => _Intensity = Mathf.Max(0.0f, value);
    }
    [Range(0.0f, 1f)]
    [SerializeField]
    private float _BlurWidth = 0.85f;
    public float BlurWidth
    {
        get => _BlurWidth;
        set => _BlurWidth = Mathf.Max(0.0f, value);
    }
    [SerializeField]
    private RenderPassEvent _RenderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    public RenderPassEvent RenderPassEvent
    {
        get => _RenderPassEvent;
        set => _RenderPassEvent = value;
    }
    [SerializeField]
    //private string _ProfilerTag = "Screen Space Volumetric Renderer Feature";
    private string _ProfilerTag = "Volumetric Lighting 2D Renderer Feature";
    public string ProfilerTag
    {
        get => _ProfilerTag;
        set => _ProfilerTag = value;
    }
}

//[DisallowMultipleRendererFeature]
//[Tooltip("Applies compression to the rendered image based on the JPEG standard")]
class VolumetricLighting2DRendererFeature : ScriptableRendererFeature
{
    // Serialized Fields
    [SerializeField, HideInInspector]
    private Shader m_Volumetric2DShader;
    [SerializeField, HideInInspector]
    private Shader m_OccludersShader;
    [SerializeField]
    private VolumetricLighting2DSettings m_Settings = new VolumetricLighting2DSettings();
    [SerializeField]
    private CameraType m_CameraType = CameraType.SceneView | CameraType.Game;

    // Private Fields
    // Private Fields
    private VolumetricLighting2DPass m_VolumetricLighting2DPass = null;
    private bool m_Initialized = false;
    private Material m_Volumetric2DMaterial;
    private Material m_OccludersMaterial;

    // Constants
    private const string k_ShaderPath = "Shaders/";
    private const string k_Volumetric2DShaderName = "VolumetricLighting2D";
    private const string k_OccludersShaderName = "Occluders";

    public VolumetricLighting2DSettings GetSettings()
    {
        return new VolumetricLighting2DSettings(m_Settings);
    }
    public void SetSettings(VolumetricLighting2DSettings settings)
    {
        m_Settings = settings;
    }

    public override void Create()
    {
        if (!RendererFeatureHelper.ValidUniversalPipeline(GraphicsSettings.defaultRenderPipeline, true, false)) return;

        m_Initialized = Initialize();

        if (m_Initialized)
            if (m_VolumetricLighting2DPass == null)
                m_VolumetricLighting2DPass = new VolumetricLighting2DPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!m_Initialized) return;

        if (!RendererFeatureHelper.CameraTypeMatches(m_CameraType, renderingData.cameraData.cameraType)) return;

        bool shouldAdd = m_VolumetricLighting2DPass.Setup(m_Settings, renderer, m_Volumetric2DMaterial, m_OccludersMaterial);
        if (shouldAdd)
        {
            renderer.EnqueuePass(m_VolumetricLighting2DPass);
        }
    }

    protected override void Dispose(bool disposing)
    {
        m_VolumetricLighting2DPass.Dispose();
        RendererFeatureHelper.DisposeMaterial(ref m_Volumetric2DMaterial);
        base.Dispose(disposing);
    }

    private bool Initialize()
    {
        if (!RendererFeatureHelper.LoadShader(ref m_Volumetric2DShader, k_ShaderPath, k_Volumetric2DShaderName)) return false;
        if (!RendererFeatureHelper.LoadShader(ref m_OccludersShader, k_ShaderPath, k_OccludersShaderName)) return false;
        if (!RendererFeatureHelper.GetMaterial(m_Volumetric2DShader, ref m_Volumetric2DMaterial)) return false;
        if (!RendererFeatureHelper.GetMaterial(m_OccludersShader, ref m_OccludersMaterial)) return false;
        return true;
    }

    class VolumetricLighting2DPass : ScriptableRenderPass
    {
        // Private Variables
        private Material m_Volumetric2DMaterial;
        private Material m_OccludersMaterial;
        private readonly List<ShaderTagId> m_ShaderTagIdList = new List<ShaderTagId>();
        //private FilteringSettings m_OccluderFilteringSettings = new FilteringSettings(RenderQueueRange.opaque);
        private FilteringSettings m_OccluderFilteringSettings = new FilteringSettings(RenderQueueRange.all);
        RenderTargetIdentifier m_OccludersTextureTarget;
        private ProfilingSampler m_ProfilingSampler = null;
        private ScriptableRenderer m_Renderer = null;
        private VolumetricLighting2DSettings m_CurrentSettings = new VolumetricLighting2DSettings();

        // Constants
        private const string k_PassProfilerTag = "Volumetric Lighting 2D Pass";

        // Statics
        private static readonly int s_OccludersTextureID = Shader.PropertyToID("_Volumetric2D_OccludersTex");

        public VolumetricLighting2DPass() 
        {
            m_ShaderTagIdList.Add(new ShaderTagId("Universal2D"));
            m_ShaderTagIdList.Add(new ShaderTagId("UniversalForward"));
            m_ShaderTagIdList.Add(new ShaderTagId("UniversalForwardOnly"));
            m_ShaderTagIdList.Add(new ShaderTagId("LightweightForward"));
            m_ShaderTagIdList.Add(new ShaderTagId("SRPDefaultUnlit"));
        }

            public bool Setup(VolumetricLighting2DSettings settings, ScriptableRenderer renderer, Material volumetricMaterial, Material occludersMaterial)
        {
            m_CurrentSettings = settings;
            m_Renderer = renderer;
            m_Volumetric2DMaterial = volumetricMaterial;
            m_OccludersMaterial = occludersMaterial;

            m_ProfilingSampler = new ProfilingSampler(k_PassProfilerTag);
            renderPassEvent = m_CurrentSettings.RenderPassEvent;
            ConfigureInput(ScriptableRenderPassInput.Color);

            if (m_Volumetric2DMaterial == null) return false;
            if (m_OccludersMaterial == null) return false;
            return true;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
        }
        
        // called each frame before Execute, use it to set up things the pass will need
        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            RenderTextureDescriptor occludersDescriptor = new RenderTextureDescriptor(
                Mathf.CeilToInt(cameraTextureDescriptor.width * m_CurrentSettings.ResolutionScale),
                Mathf.CeilToInt(cameraTextureDescriptor.height * m_CurrentSettings.ResolutionScale),
                cameraTextureDescriptor.colorFormat
                );
            occludersDescriptor.depthBufferBits = 0;
            cmd.GetTemporaryRT(s_OccludersTextureID, occludersDescriptor, FilterMode.Bilinear);
            m_OccludersTextureTarget = new RenderTargetIdentifier(s_OccludersTextureID);
            ConfigureTarget(m_OccludersTextureTarget);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // fetch a command buffer to use
            CommandBuffer cmd = CommandBufferPool.Get(m_CurrentSettings.ProfilerTag);
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                //cmd.SetRenderTarget(m_OccludersTextureTarget);

                cmd.Blit(m_CurrentSettings.LightTexture, m_OccludersTextureTarget);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                DrawingSettings drawSettings = CreateDrawingSettings(m_ShaderTagIdList, ref renderingData, SortingCriteria.CommonTransparent);
                drawSettings.overrideMaterial = m_OccludersMaterial;

                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref m_OccluderFilteringSettings);

                m_Volumetric2DMaterial.SetVector("_Center", new Vector2(
                    (m_CurrentSettings.LightScreenPosition.x + 1.0f) / 2.0f,
                    (m_CurrentSettings.LightScreenPosition.y + 1.0f) / 2.0f));
                m_Volumetric2DMaterial.SetFloat("_Intensity", m_CurrentSettings.Intensity);
                m_Volumetric2DMaterial.SetFloat("_BlurWidth", m_CurrentSettings.BlurWidth);
                m_Volumetric2DMaterial.SetFloat("_Samples", m_CurrentSettings.Samples);

                // then blit back into color target 
                cmd.Blit(m_OccludersTextureTarget, m_Renderer.cameraColorTarget, m_Volumetric2DMaterial, 0);
            }

            // don't forget to tell ScriptableRenderContext to actually execute the commands
            context.ExecuteCommandBuffer(cmd);

            // tidy up after ourselves
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // Release Temporary RT here
            cmd.ReleaseTemporaryRT(s_OccludersTextureID);
        }

        public void Dispose()
        {
            // Dispose of buffers here
            // this pass doesnt have any buffers
        }
    }
}
