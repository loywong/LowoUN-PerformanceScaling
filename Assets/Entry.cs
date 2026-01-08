using System.Collections;
using LowoUN.Module.Perf;
using LowoUN.Tool;
using LowoUN.Util;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class Entry : MonoBehaviour {
    float alignY;
    bool hasInit;

    void Awake () {
        DontDestroyOnLoad (gameObject);

        hasInit = false;

        alignY = Screen.height  /  2;
    }

    IEnumerator Start () {
        PerfManager.Self.Init (GetComponent<Debug_FPS> ());
        DebugConsole.Self.Init ();
        yield return null;

        Camera.main.GetComponent<UniversalAdditionalCameraData> ().renderPostProcessing = true;
        PerfManager.Self.Set_PostProcessing ();

        yield return null;

        PerfManager.Self.Check_SetManiCameraPostProcessAndFXAA ();

        hasInit = true;
    }

    void OnGUI () {
        if (!hasInit) return;

        if (PerfManager.Self.CurPerfLevelType >= PerfLevelType.High) {
            if (GUI.Button (new Rect (10, alignY + 60, 200, 42), $"场景后效:{(GameSettings.IsHideOrShowPostProcess? "off" : "on")}")) {
                PerfManager.Self.TEST_Toggle_PostProcess ();
            }
        }
    }
}