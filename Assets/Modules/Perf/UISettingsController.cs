using System;
using LowoUN.Module.Perf;
using LowoUN.Util;

[Serializable]
public class SettingDataProfile {
    public int GraphicsQuality;
    public bool IsSoundEffectOn;
    public bool IsSoundMusicOn;
    public int LanguageType;
}

public class UISettingsController : SingletonSimple<UISettingsController> {
    // public UISettings View;
    private SettingDataProfile _settingDataProfile;
#if !SRV_ALIYUN_PRODUCTION
    bool hasRuntimeCustomGraphicsQuality;
#endif
    // public MultilingualTypeEnum GetLanguage() => (MultilingualTypeEnum)_settingDataProfile.LanguageType;
    // public void SetLanguage(MultilingualTypeEnum languageType)=>_settingDataProfile.LanguageType = (int)languageType;

#if !SRV_ALIYUN_PRODUCTION
    public PerfLevelType GetPerfLevelType () {
        if (GameSettings._instance.isQualityLevel_SettingPanel) {
            return (PerfLevelType) _settingDataProfile.GraphicsQuality;
        } else {
            if (hasRuntimeCustomGraphicsQuality)
                return (PerfLevelType) _settingDataProfile.GraphicsQuality;
            else
                return GameSettings._instance.ForceQualityLevel;
        }
    }
#else
    public PerfLevelType GetPerfLevelType () => (PerfLevelType) _settingDataProfile.GraphicsQuality;
#endif

    public void SetPerfLevelType (PerfLevelType perfLevelType) {
        _settingDataProfile.GraphicsQuality = (int) perfLevelType;
#if !SRV_ALIYUN_PRODUCTION
        hasRuntimeCustomGraphicsQuality = true;
#endif
        //UISettings.Self.RefreshGraphics();
        UISettingsController.Self.SaveData ();
        PerfManager.Self.RefreshAll ();
        // Fn_TeDot.Self.ChangePerfLevel();
    }

    public bool GetMusicFlag () => _settingDataProfile.IsSoundMusicOn;
    public bool GetSoundEffectFlag () => _settingDataProfile.IsSoundEffectOn;

    // public void SetMusic(bool isOn)
    // {
    //     _settingDataProfile.IsSoundMusicOn = isOn;
    //     Fn_Audio.Self.SetBgMusicEnabled(isOn);
    // }

    // public void SetSoundEffect(bool isOn)
    // {
    //     _settingDataProfile.IsSoundEffectOn = isOn;
    //     Fn_Audio.Self.SetSoundEffectEnabled(isOn);
    // }
    public void InitData () {
        if (_settingDataProfile == null) {
            _settingDataProfile = new SettingDataProfile ();
            bool ifSuccess = DataSaveManager.Self.LoadLocalData (_settingDataProfile);
            if (!ifSuccess) {
                //通过硬件性能调整
                _settingDataProfile.GraphicsQuality = (int) PerfManager.Self.GetPerfLevel_FirstLaunchApp ();

                // _settingDataProfile.IsSoundEffectOn = true;
                // _settingDataProfile.IsSoundMusicOn = true;
                // _settingDataProfile.LanguageType = Application.systemLanguage switch
                // {
                //     SystemLanguage.Chinese => (int)MultilingualTypeEnum.CHS,
                //     SystemLanguage.ChineseSimplified => (int)MultilingualTypeEnum.CHS,
                //     SystemLanguage.ChineseTraditional => (int)MultilingualTypeEnum.CHT,
                //     _ => (int)MultilingualTypeEnum.EN
                // };

                // DataSaveManager.Self.SaveDataToLocal(_settingDataProfile);
            }
        }
    }

    public void SaveData () {
        //     DataSaveManager.Self.SaveDataToLocal(_settingDataProfile);
    }
}