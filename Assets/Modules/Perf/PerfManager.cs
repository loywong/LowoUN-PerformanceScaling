// SPDX-FileCopyrightText: 2026 loywong Contributors
// SPDX-License-Identifier: MIT

using System;
using System.Text.RegularExpressions;
using LowoUN.Module.Perf;
using LowoUN.Util;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Detects device capabilities and applies the appropriate runtime performance profile.
/// </summary>
public class PerfManager : SingletonSimple<PerfManager> {
    private const int TargetFrameRate = 60;

    // Conservative fallback thresholds; production projects should tune them with device telemetry.
#if UNITY_IOS
    private const float LowMemoryThresholdGb = 3.0f;
    private const float HighMemoryThresholdGb = 5.4f;
#else
    private const float LowMemoryThresholdGb = 4.0f;
    private const float HighMemoryThresholdGb = 6.0f;
#endif

    private UniversalRenderPipelineAsset _selectedPipeline;
    private float _deviceMemoryGb;
    private int _cpuProcessorCount;
    private int _cpuProcessorFrequency;
    private string _cpuProcessorType;
    private string _graphicsDeviceName;

    // Keep the hardware-derived baseline separate from the level selected by the player.
    private PerfLevelType _detectedPerfLevelType;
    private PerfLevelType _currentPerfLevelType;
    private bool _isIPhone;
    private int _currentMaxScreenParticle;

    /// <summary>Raised after a user-selected performance level has been fully applied.</summary>
    public event Action OnResetPerfLev;

    public float DeviceMemoryGB => _deviceMemoryGb;
    public int CpuProcessorCount => _cpuProcessorCount;
    public int CpuProcessorFrequency => _cpuProcessorFrequency;
    public string CpuProcessorType => _cpuProcessorType;
    public string GraphicsDeviceName => _graphicsDeviceName;

    /// <summary>The hardware-derived level before any user override is applied.</summary>
    public PerfLevelType CurPerfLevelType_Real => _detectedPerfLevelType;

    /// <summary>The performance level currently applied to the application.</summary>
    public PerfLevelType CurPerfLevelType => _currentPerfLevelType;
    public int MaxScreenParticle => _currentMaxScreenParticle;

#if !SRV_ALIYUN_PRODUCTION
    public string perfLevDesc { private set; get; }
#endif

    public PerfLevelType memLev { private set; get; }
    public PerfLevelType cpuLev { private set; get; }
    public PerfLevelType gpuLev { private set; get; }

    // Public diagnostics retained for compatibility with the existing debug UI.
    public float cpuPerformanceScore;
    public string gpuName;
    public int gpuMemoryMB;
    public bool hasTessellation;
    public bool hasGeometryShaders;
    public bool hasComputeShaders;
    public int maxTextureSize;
    public bool astcSupport;
    public bool etc2Support;
    public bool pvrtcSupport;
    public int graphicsShaderLevel;
    public float gpuPerformanceScore;

#if UNITY_ANDROID && !UNITY_EDITOR
    private readonly float _defaultRenderScaleHigh = PerfSettings.default_renderScale_Android_high;
    private readonly float _defaultRenderScaleMid = PerfSettings.default_renderScale_Android_mid;
    private readonly float _defaultRenderScaleLow = PerfSettings.default_renderScale_Android_low;
#elif UNITY_IOS && !UNITY_EDITOR
    private readonly float _defaultRenderScaleHigh = PerfSettings.default_renderScale_iOS_high;
    private readonly float _defaultRenderScaleMid = PerfSettings.default_renderScale_iOS_mid;
    private readonly float _defaultRenderScaleLow = PerfSettings.default_renderScale_iOS_low;
#else
    private readonly float _defaultRenderScaleHigh = PerfSettings.default_renderScale_high;
    private readonly float _defaultRenderScaleMid = PerfSettings.default_renderScale_mid;
    private readonly float _defaultRenderScaleLow = PerfSettings.default_renderScale_low;
#endif

    /// <summary>
    /// Initializes performance settings without running hardware detection in the Editor.
    /// </summary>
    public void Init_DebugMode() {
        _isIPhone = false;

        if (GameSettings._instance.ForceQualityLevel != PerfLevelType.NONE) {
            _currentPerfLevelType = GameSettings._instance.ForceQualityLevel;
        } else {
#if UNITY_EDITOR
            _currentPerfLevelType = PerfLevelType.High;
#else
            CacheDeviceInfo ();
            UpdatePerformanceLevel ();
#endif
        }

        ApplyInitialSettings();
    }

    /// <summary>
    /// Initializes performance settings from a previously selected or cached quality level.
    /// </summary>
    public void Init(PerfLevelType cachePerfLevType) {
        _detectedPerfLevelType = GetPerfLevel_FirstLaunchApp();
        PerfLevelHandler.Self.SetOutlinePassEnabled(_detectedPerfLevelType > PerfLevelType.Low);

        LLog.NOT_PRODUCTION_LOG($"[Perf] Initializing with cached level: {cachePerfLevType}");
        _currentPerfLevelType = cachePerfLevType;

        ApplyInitialSettings();
    }

    private void ApplyInitialSettings() {
        LLog.NOT_PRODUCTION_LOG(
            $"[Perf] Applying level {_currentPerfLevelType}; " +
            $"Unity quality level: {QualitySettings.GetQualityLevel()}");

#if !SRV_ALIYUN_PRODUCTION
        perfLevDesc = BuildPerformanceDescription();
        LLog.NOT_PRODUCTION_LOG($"[Perf] {perfLevDesc}");
#endif

        ApplyPerformanceProfile(_currentPerfLevelType);
        ResetRenderScale();
        Init_MaxScreenParticle();

        Application.runInBackground = true;
        LLog.NOT_PRODUCTION_LOG(
            $"[Perf] Run in background: {Application.runInBackground}");
    }

#if !SRV_ALIYUN_PRODUCTION
    private string BuildPerformanceDescription() {
        return $"Memory: {DeviceMemoryGB:F1} GB [{memLev}], " +
        $"CPU: {CpuProcessorType} ({_cpuProcessorCount} cores, {_cpuProcessorFrequency} MHz, " +
        $"score {cpuPerformanceScore:F1}) [{cpuLev}], " +
        $"GPU: {GraphicsDeviceName} (score {gpuPerformanceScore:F1}) [{gpuLev}]";
    }
#endif

    /// <summary>
    /// Restores the particle limit associated with the active performance level.
    /// </summary>
    public void Init_MaxScreenParticle() {
        _currentMaxScreenParticle = _currentPerfLevelType
        switch {
            PerfLevelType.High => PerfSettings.MaxScreenParticle_High,
            PerfLevelType.Mid => PerfSettings.MaxScreenParticle_Middle,
            _ => PerfSettings.MaxScreenParticle_Low
        };
    }

    public void SetMaxScreenParticle(int value) {
        _currentMaxScreenParticle = value;
    }

    /// <summary>
    /// Detects the recommended performance level from memory, CPU, and GPU capabilities.
    /// The lowest component tier determines the final result.
    /// </summary>
    public PerfLevelType GetPerfLevel_FirstLaunchApp() {
        DetectIPhone();
        CacheDeviceInfo();
        UpdateComponentTiers();

        PerfLevelType level = GetLowestTier(memLev, cpuLev, gpuLev);
        LLog.NOT_PRODUCTION_LOG(
            $"[Perf] Detected level: {level} " +
            $"(memory: {memLev}, CPU: {cpuLev}, GPU: {gpuLev})");

        return level;
    }

    private void DetectIPhone() {
#if UNITY_IOS
        _isIPhone = true;
#else
        _isIPhone = false;
#endif
        LLog.NOT_PRODUCTION_LOG($"[Perf] Running on iPhone: {_isIPhone}");
    }

    private void CacheDeviceInfo() {
        _deviceMemoryGb = SystemInfo.systemMemorySize / 1024f;
        _cpuProcessorType = SystemInfo.processorType;
        _cpuProcessorCount = SystemInfo.processorCount;
        _cpuProcessorFrequency = SystemInfo.processorFrequency;
        _graphicsDeviceName = SystemInfo.graphicsDeviceName;
    }

    private void UpdatePerformanceLevel() {
        UpdateComponentTiers();
        _currentPerfLevelType = GetLowestTier(memLev, cpuLev, gpuLev);

        LLog.NOT_PRODUCTION_LOG(
            $"[Perf] Updated level: {_currentPerfLevelType} " +
            $"(memory: {memLev}, CPU: {cpuLev}, GPU: {gpuLev})");
    }

    private void UpdateComponentTiers() {
        memLev = DetermineMemoryTier();

        // On iOS, graphicsDeviceName carries the Apple chip model used by the CPU heuristic.
        gpuLev = DetermineGpuTier();
        cpuLev = DetermineCpuTier();
    }

    private static PerfLevelType GetLowestTier(
        PerfLevelType memoryTier,
        PerfLevelType cpuTier,
        PerfLevelType gpuTier) {
        PerfLevelType level = PerfLevelType.High;

        if (memoryTier < level) {
            level = memoryTier;
        }

        if (cpuTier < level) {
            level = cpuTier;
        }

        if (gpuTier < level) {
            level = gpuTier;
        }

        return level;
    }

    private void ApplyPerformanceProfile(PerfLevelType level) {
        UniversalRenderPipelineAsset pipeline;

        switch (level) {
            case PerfLevelType.Low:
                pipeline = GameSettings._instance.LowQualityPipeline;
                break;
            case PerfLevelType.Mid:
                pipeline = GameSettings._instance.MediumQualityPipeline;
                break;
            case PerfLevelType.High:
                pipeline = GameSettings._instance.HighQualityPipeline;
                break;
            default:
                LLog.NOT_PRODUCTION_ERROR(
                    $"[Perf] Cannot apply unsupported performance level: {level}");
                return;
        }

        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFrameRate;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        _selectedPipeline = pipeline;

        // Assigning the current pipeline again rebuilds URP and can trigger shader recompilation.
        if (_selectedPipeline != null &&
            GraphicsSettings.currentRenderPipeline != _selectedPipeline) {
            GraphicsSettings.renderPipelineAsset = _selectedPipeline;
        }

        ApplyUnityQualityLevel();
    }

    private void ApplyUnityQualityLevel() {
        // These indices must stay aligned with the order in ProjectSettings/QualitySettings.asset.
        int qualityLevel = _currentPerfLevelType
        switch {
            PerfLevelType.Low => 1,
            PerfLevelType.Mid => 2,
            PerfLevelType.High => 5,
            _ => 0
        };

        if (QualitySettings.GetQualityLevel() != qualityLevel) {
            QualitySettings.SetQualityLevel(qualityLevel, true);
        }
    }

    private PerfLevelType DetermineMemoryTier() {
        LLog.NOT_PRODUCTION_LOG(
            $"[Perf] System memory: {_deviceMemoryGb:F1} GB");

        if (_deviceMemoryGb <= LowMemoryThresholdGb) {
            return PerfLevelType.Low;
        }

        return _deviceMemoryGb <= HighMemoryThresholdGb ?
            PerfLevelType.Mid :
            PerfLevelType.High;
    }

    private PerfLevelType DetermineCpuTier() {
        float frequencyGhz = _cpuProcessorFrequency / 1000f;

        if (_isIPhone) {
            cpuPerformanceScore = CalculateIPhoneCpuScore(gpuName);
            LLog.NOT_PRODUCTION_LOG(
                $"[Perf] iPhone CPU score: {cpuPerformanceScore:F1}; " +
                $"{CpuProcessorType}, {_cpuProcessorCount} cores, {frequencyGhz:F2} GHz");

            return GetIPhoneCpuTier(cpuPerformanceScore);
        }

        cpuPerformanceScore = CalculateCpuScore(_cpuProcessorCount, frequencyGhz);
        LLog.NOT_PRODUCTION_LOG(
            $"[Perf] CPU score: {cpuPerformanceScore:F1}; " +
            $"{CpuProcessorType}, {_cpuProcessorCount} cores, {frequencyGhz:F2} GHz");

        return GetCpuTier(cpuPerformanceScore);
    }

    private static float CalculateCpuScore(int coreCount, float frequencyGhz) {
        // Mobile-oriented heuristic: core count contributes 60%, clock speed contributes 40%.
        float coreScore = Mathf.Clamp(coreCount / 8f, 0f, 1f) * 10f;
        float frequencyScore = Mathf.Clamp(frequencyGhz / 3.8f, 0f, 1f) * 10f;
        return (coreScore * 0.6f) + (frequencyScore * 0.4f);
    }

    private static float CalculateIPhoneCpuScore(string deviceName) {
        // processorType is often generic on iOS; the GPU name usually exposes the Apple SoC model.
        string normalizedName = deviceName?.ToLowerInvariant() ?? string.Empty;

        if (normalizedName.Contains("apple m")) {
            return 10f;
        }

        if (!normalizedName.Contains("apple a")) {
            return 6f;
        }

        int version = ExtractAppleChipVersion(normalizedName);

        if (version >= 14) {
            return 10f;
        }

        if (version >= 10) {
            return 8.2f;
        }

        if (version >= 7) {
            return 7.5f;
        }

        if (version >= 4) {
            return 6.5f;
        }

        return 6f;
    }

    private static PerfLevelType GetIPhoneCpuTier(float score) {
        if (score >= 9f) {
            return PerfLevelType.High;
        }

        return score >= 7f ? PerfLevelType.Mid : PerfLevelType.Low;
    }

    private static PerfLevelType GetCpuTier(float score) {
        if (score >= 9.5f) {
            return PerfLevelType.High;
        }

        return score >= 9f ? PerfLevelType.Mid : PerfLevelType.Low;
    }

    private PerfLevelType DetermineGpuTier() {
        CacheGpuCapabilities();
        gpuPerformanceScore = CalculateGpuScore();

        LLog.NOT_PRODUCTION_LOG(
            $"[Perf] GPU score: {gpuPerformanceScore:F1}; {gpuName}, " +
            $"{gpuMemoryMB} MB, shader level {graphicsShaderLevel}, " +
            $"max texture {maxTextureSize}, tessellation {hasTessellation}, " +
            $"geometry {hasGeometryShaders}, compute {hasComputeShaders}, " +
            $"ASTC {astcSupport}, ETC2 {etc2Support}, PVRTC {pvrtcSupport}");

        return GetGpuTier(gpuPerformanceScore);
    }

    private void CacheGpuCapabilities() {
        gpuName = SystemInfo.graphicsDeviceName;
        gpuMemoryMB = SystemInfo.graphicsMemorySize;
        hasTessellation = SystemInfo.supportsTessellationShaders;
        hasGeometryShaders = SystemInfo.supportsGeometryShaders;
        hasComputeShaders = SystemInfo.supportsComputeShaders;
        maxTextureSize = SystemInfo.maxTextureSize;
        graphicsShaderLevel = SystemInfo.graphicsShaderLevel;
        astcSupport = SystemInfo.SupportsTextureFormat(TextureFormat.ASTC_6x6);
        etc2Support = SystemInfo.SupportsTextureFormat(TextureFormat.ETC2_RGBA8);
        pvrtcSupport = SystemInfo.SupportsTextureFormat(TextureFormat.PVRTC_RGBA4);
    }

    private PerfLevelType GetGpuTier(float score) {
        // Empirical thresholds should be recalibrated when the scoring weights change.
        if (_isIPhone) {
            if (score >= 90f) {
                return PerfLevelType.High;
            }

            return score >= 72f ? PerfLevelType.Mid : PerfLevelType.Low;
        }

        if (score >= 96f) {
            return PerfLevelType.High;
        }

        return score >= 80f ? PerfLevelType.Mid : PerfLevelType.Low;
    }

    private float CalculateGpuScore() {
        if (_isIPhone) {
            return CalculateIPhoneGpuScore();
        }

        float score = 0f;

        // Score budget: memory 25, shader level 20, features 10,
        // texture size 15, and recognized GPU family 40.
        score += Mathf.Clamp(gpuMemoryMB / 2048f, 0f, 1f) * 25f;
        score += Mathf.Clamp((graphicsShaderLevel - 20) / 50f, 0f, 1f) * 20f;

        float featureScore = 0f;
        if (hasTessellation) {
            featureScore += 0.3f;
        }

        if (hasGeometryShaders) {
            featureScore += 0.2f;
        }

        if (hasComputeShaders) {
            featureScore += 0.3f;
        }

        if (astcSupport) {
            featureScore += 0.2f;
        }

        score += featureScore * 10f;
        score += Mathf.Clamp(maxTextureSize / 8192f, 0f, 1f) * 15f;
        score += GetGpuModelBonus(gpuName) * 40f;

        return score;
    }

    private float CalculateIPhoneGpuScore() {
        float score = 0f;

        // Score budget: memory 20, shader level 15, features 10,
        // texture size 15, and recognized Apple chip family 40.
        score += Mathf.Clamp(gpuMemoryMB / 2048f, 0f, 1f) * 20f;
        score += Mathf.Clamp((graphicsShaderLevel - 20) / 40f, 0f, 1f) * 15f;

        float featureScore = 0f;
        if (hasTessellation) {
            featureScore += 0.25f;
        }

        if (hasGeometryShaders) {
            featureScore += 0.2f;
        }

        if (hasComputeShaders) {
            featureScore += 0.25f;
        }

        if (astcSupport) {
            featureScore += 0.3f;
        }

        score += featureScore * 10f;
        score += Mathf.Clamp(maxTextureSize / 8192f, 0f, 1f) * 15f;
        score += GetIPhoneGpuModelBonus(gpuName) * 40f;

        return score;
    }

    private static float GetIPhoneGpuModelBonus(string deviceName) {
#if UNITY_EDITOR
        // Keep Editor results deterministic and independent of the development machine's GPU.
        return 1f;
#else
        string normalizedName = deviceName?.ToLowerInvariant () ?? string.Empty;

        if (normalizedName.Contains ("apple m")) {
            return 1f;
        }

        if (!normalizedName.Contains ("apple a")) {
            return 0.3f;
        }

        int version = ExtractAppleChipVersion (normalizedName);

        if (version >= 14) {
            return 1f;
        }

        if (version >= 10) {
            return 0.75f;
        }

        if (version >= 7) {
            return 0.55f;
        }

        if (version >= 4) {
            return 0.35f;
        }

        return 0.3f;
#endif
    }

    private static int ExtractAppleChipVersion(string input) {
        Match match = Regex.Match(input, @"apple a(\d+)");
        return match.Success && int.TryParse(match.Groups[1].Value, out int version) ?
            version :
            0;
    }

    private static float GetGpuModelBonus(string deviceName) {
#if UNITY_EDITOR
        // Keep Editor results deterministic and independent of the development machine's GPU.
        return 1f;
#else
        string normalizedName = deviceName?.ToLowerInvariant () ?? string.Empty;

        bool isHighEnd =
            normalizedName.Contains ("adreno (tm) 9") ||
            normalizedName.Contains ("adreno (tm) 8") ||
            normalizedName.Contains ("adreno (tm) 750") ||
            normalizedName.Contains ("adreno (tm) 740") ||
            normalizedName.Contains ("mali-g7") ||
            normalizedName.Contains ("mali-g8");

        if (isHighEnd) {
            return 1f;
        }

        bool isMidRange =
            normalizedName.Contains ("adreno (tm) 730") ||
            normalizedName.Contains ("adreno (tm) 6") ||
            normalizedName.Contains ("mali-t8") ||
            normalizedName.Contains ("powervr g") ||
            normalizedName.Contains ("apple a9") ||
            normalizedName.Contains ("apple a10");

        return isMidRange ? 0.6f : 0.3f;
#endif
    }

    private void ResetRenderScale() {
        if (_selectedPipeline == null) {
            LLog.NOT_PRODUCTION_ERROR("[Perf] URP asset is not configured.");
            return;
        }

        float renderScale;

        switch (_currentPerfLevelType) {
            case PerfLevelType.High:
                renderScale = _defaultRenderScaleHigh;
                break;
            case PerfLevelType.Mid:
                renderScale = _defaultRenderScaleMid;
                break;
            case PerfLevelType.Low:
                renderScale = _defaultRenderScaleLow;
                break;
            default:
                return;
        }

        SetRenderScale(renderScale);
    }

    /// <summary>
    /// Applies the temporary render-scale reduction used during battle.
    /// </summary>
    public void Set_Battle_In() {
        // Small fixed reductions lower fill-rate cost without changing the selected base profile.
        float renderScale;

        switch (_currentPerfLevelType) {
            case PerfLevelType.High:
                renderScale = _defaultRenderScaleHigh - 0.05f;
                break;
            case PerfLevelType.Mid:
                renderScale = _defaultRenderScaleMid - 0.1f;
                break;
            case PerfLevelType.Low:
                renderScale = _defaultRenderScaleLow - 0.05f;
                break;
            default:
                return;
        }

        SetRenderScale(renderScale);
    }

    /// <summary>
    /// Restores the render scale after leaving battle.
    /// </summary>
    public void Set_Battle_Out() {
        ResetRenderScale();
    }

    private void SetRenderScale(float targetScale) {
        if (_selectedPipeline == null) {
            LLog.NOT_PRODUCTION_ERROR("[Perf] URP asset is not configured.");
            return;
        }

        LLog.Green($"[Perf] Render scale: {targetScale:F2}");
        _selectedPipeline.renderScale = targetScale;
    }

    /// <summary>
    /// Applies the quality selected in the settings UI and notifies dependent systems.
    /// </summary>
    public void RefreshAll() {
        _currentPerfLevelType = UISettingsController.Self.GetPerfLevelType();

        ApplyPerformanceProfile(_currentPerfLevelType);
        ResetRenderScale();
        Init_MaxScreenParticle();

        PerfLevelHandler.Self.PerfLevChanged();
        OnResetPerfLev?.Invoke();
    }
}
