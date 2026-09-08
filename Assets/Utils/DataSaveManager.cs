using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using LowoUN.Util;
using UnityEngine;

public class DataSaveManager : SingletonSimple<DataSaveManager> {
    static readonly object localDataFileLock = new object ();
    string _Path_role_dev = null;
    string Path_role_dev {
        get {
            if (!string.IsNullOrEmpty (_Path_role_dev)) _Path_role_dev = $"{Application.persistentDataPath}/Project_Dev";
            return _Path_role_dev;
        }
    }
    string _Path_role_loc = null;
    string Path_role_loc {
        get {
            if (!string.IsNullOrEmpty (_Path_role_loc)) _Path_role_loc = $"{Application.persistentDataPath}/Project_Loc";
            return _Path_role_loc;
        }
    }

    public void ClearAllData_Player () {
#if UNITY_EDITOR
        DeleteDirectory_Dev ();
#endif
        DeleteDirectory_Loc ();
    }

    void DeleteDirectory_Dev () {
        try {
            if (Directory.Exists (Path_role_dev)) {
                Directory.Delete (Path_role_dev, true);
                Debug.Log ($"目录 删除成功 {Path_role_dev}");
            } else {
                Debug.LogWarning ($"目录 不存在 {Path_role_dev}");
            }
        } catch (System.Exception e) {
            Debug.LogError ($"删除目录异常: {e.Message}");
        }
    }
    void DeleteDirectory_Loc () {
        lock (localDataFileLock) {
            try {
                if (Directory.Exists (Path_role_loc)) {
                    Directory.Delete (Path_role_loc, true);
                    Debug.Log ($"目录 删除成功 {Path_role_loc}");
                } else {
                    Debug.LogWarning ($"目录 不存在 {Path_role_loc}");
                }
            } catch (System.Exception e) {
                Debug.LogError ($"删除目录异常: {e.Message}");
            }
        }
    }

    // 清理账号和角色信息
    public void ClearAllData_Account () {
        LLog.NOT_PRODUCTION_LOG ("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!! PlayerPrefs.DeleteAll()");
        PlayerPrefs.DeleteAll ();
    }
    //Server登录时清理Dev本地数据
    public void ClearDevData_AtServerLogin () {
        if (Directory.Exists (Path_role_dev)) {
            Directory.Delete (Path_role_dev, true);
        }
    }

    /// <summary>
    /// 仅测试时使用==》本地存储游戏数据
    /// </summary>
    /// <param name="data">存储数据类型(存储数据类)</param>
    public void SaveData (object data, Action saveSuccess = null) {
        saveSuccess?.Invoke ();
        // return;

        // // if (GameSettings._instance.isUseServer) { saveSuccess?.Invoke (); return; } //使用server时禁用local数据存储

        // // string folderName = GameRoleDev;
        // SaveDataReal (Path_role_dev, data, saveSuccess);
    }

    /// <summary>
    /// 正式使用==》处理需要保存在本地的数据
    /// </summary>
    /// <param name="data">存储数据类型(存储数据类)</param>
    public void SaveDataToLocal (object data, Action saveSuccess = null) {
        // string folderName = $"{GameName}_Loc";
        SaveDataReal (Path_role_loc, data, saveSuccess);
    }
    // void SaveDataReal (string folderName, object data, Action saveSuccess = null) {
    void SaveDataReal (string path, object data, Action saveSuccess) {
        if (data == null) {
            Debug.LogWarning ("SaveDataReal: data is null");
            return;
        }

        // if (Utils.StringIsNullOrEmpty (DataSavePath)) { GetSavePath (); }

        string saveName = data.GetType ().Name; //获取唯一存储标志名（存储类名）
        //Log.Error($"__DataSaveManager__ SaveDataReal () saveName:{saveName}");
        string filePath = path + $"/{saveName}.txt";
        var json = JsonUtility.ToJson (data);

        lock (localDataFileLock) {
            try {
                if (!Directory.Exists (path)) {
                    Directory.CreateDirectory (path);
                }
                using (FileStream file = File.Open (filePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                    BinaryFormatter formatter = new BinaryFormatter (); //进行二进制转化
                    formatter.Serialize (file, json);
                }
            } catch (IOException e) {
                Debug.LogWarning ($"SaveDataReal file is busy, path:{filePath}, error:{e.Message}");
                return;
            } catch (Exception e) {
                Debug.LogError ($"SaveDataReal failed, path:{filePath}, error:{e.Message}");
                return;
            }
        }
        saveSuccess?.Invoke ();
    }

    /// 仅测试时使用==》读取本地存储数据
    public bool LoadData (object loadData) {
        return false;

        // if (GameSettings._instance.isUseServer) { return false; } //使用server时禁用local数据存储
        // bool ifGetInfo = false;
        // // if (Utils.StringIsNullOrEmpty (DataSavePath)) { GetSavePath (); }

        // string loadName = loadData.GetType ().Name; //获取唯一读取数据标志名
        // BinaryFormatter bf = new BinaryFormatter ();
        // if (File.Exists (Path_role_dev + $"/{loadName}.txt")) {
        //     FileStream file = File.Open (Path_role_dev + $"/{loadName}.txt", FileMode.Open);
        //     JsonUtility.FromJsonOverwrite ((string) bf.Deserialize (file), loadData);
        //     file.Close ();
        //     ifGetInfo = true;
        // }

        // //Log.Error($"__DataSaveManager__ LoadData (), ifGetInfo:{ifGetInfo}");

        // return ifGetInfo;
    }
    /// 正式使用==》读取本地存储数据
    public bool LoadLocalData (object loadData) {
        if (loadData == null) {
            Debug.LogError ("LoadLocalData: loadData is null");
            return false;
        }

        // if (Utils.StringIsNullOrEmpty(DataSavePath)) { GetSavePath(); }

        string loadName = loadData.GetType ().Name; //获取唯一读取数据标志名
        string filePath = Path_role_loc + $"/{loadName}.txt";
        lock (localDataFileLock) {
            if (!File.Exists (filePath)) return false;

            try {
                using (FileStream file = File.Open (filePath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                    BinaryFormatter bf = new BinaryFormatter ();
                    JsonUtility.FromJsonOverwrite ((string) bf.Deserialize (file), loadData);
                }
                return true;
            } catch (IOException e) {
                Debug.LogWarning ($"LoadLocalData file is busy, path:{filePath}, error:{e.Message}");
            } catch (Exception e) {
                Debug.LogError ($"LoadLocalData failed, path:{filePath}, error:{e.Message}");
            }
        }

        //Log.Error($"__DataSaveManager__ LoadLocalData (): ifGetInfo {ifGetInfo}");
        return false;
    }

    #region  异步 读写 存盘 战斗中数据
    public void SaveData_Async (object data, Action saveSuccess = null) {
        var path = Path_role_loc;

        if (data == null) {
            Debug.LogWarning ("SaveData_Async: data is null");
            saveSuccess?.Invoke ();
            return;
        }

        string saveName = data.GetType ().Name;
        // Serialize to JSON on main thread (JsonUtility is Unity API)
        string json = JsonUtility.ToJson (data);

        Task.Run (() => {
            string filePath = path + $"/{saveName}.txt";
            lock (localDataFileLock) {
                try {
                    if (!Directory.Exists (path)) {
                        Directory.CreateDirectory (path);
                    }
                    using (FileStream file = File.Open (filePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                        BinaryFormatter formatter = new BinaryFormatter ();
                        formatter.Serialize (file, json);
                    }
                } catch (IOException e) {
                    Debug.LogWarning ($"SaveData_Async file is busy, path:{filePath}, error:{e.Message}");
                } catch (Exception e) {
                    Debug.LogError ($"SaveData_Async failed, path:{filePath}, error:{e.Message}");
                }
            }
        }).ContinueWith (t => {
            if (t.IsFaulted) {
                Debug.LogError ("SaveData_Async task faulted");
            }
            // Try to invoke callback on main thread if possible
            try {
                saveSuccess?.Invoke ();
            } catch (Exception e) {
                Debug.LogError ($"SaveData_Async callback threw: {e.Message}");
            }
        }, TaskScheduler.FromCurrentSynchronizationContext ());
    }

    public void LoadData_Async (object data, Action saveSuccess = null) {
        var path = Path_role_loc;

        if (data == null) {
            Debug.LogWarning ("LoadData_Async: data is null");
            saveSuccess?.Invoke ();
            return;
        }

        string loadName = data.GetType ().Name;
        string filePath = path + $"/{loadName}.txt";

        Task.Run (() => {
            lock (localDataFileLock) {
                if (!File.Exists (filePath)) {
                    return (string) null;
                }

                try {
                    using (FileStream file = File.Open (filePath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                        BinaryFormatter bf = new BinaryFormatter ();
                        return (string) bf.Deserialize (file);
                    }
                } catch (IOException e) {
                    Debug.LogWarning ($"LoadData_Async file is busy, path:{filePath}, error:{e.Message}");
                    return (string) null;
                } catch (Exception e) {
                    Debug.LogError ($"LoadData_Async failed, path:{filePath}, error:{e.Message}");
                    return (string) null;
                }
            }
        }).ContinueWith (t => {
            if (t.IsFaulted) {
                Debug.LogError ("LoadData_Async task faulted");
            }

            string json = t.Result;
            if (!string.IsNullOrEmpty (json)) {
                try {
                    JsonUtility.FromJsonOverwrite (json, data);
                } catch (Exception e) {
                    Debug.LogError ($"LoadData_Async deserialize failed: {e.Message}");
                }
            }

            try {
                saveSuccess?.Invoke ();
            } catch (Exception e) {
                Debug.LogError ($"LoadData_Async callback threw: {e.Message}");
            }
        }, TaskScheduler.FromCurrentSynchronizationContext ());
    }

    public bool DeleteProfile (object data) {
        if (data == null) return false;

        var path = Path_role_loc;

        string saveName = data.GetType ().Name; //获取唯一存储标志名（存储类名）
        //Log.Error($"__DataSaveManager__ SaveDataReal () saveName:{saveName}");
        string filePath = path + $"/{saveName}.txt";

        lock (localDataFileLock) {
            if (!Directory.Exists (path)) return false;

            try {
                File.Delete (filePath);
                return true;
            } catch (IOException e) {
                Debug.LogWarning ($"DeleteProfile file is busy, path:{filePath}, error:{e.Message}");
                return false;
            } catch (Exception e) {
                Debug.LogError ($"DeleteProfile failed, path:{filePath}, error:{e.Message}");
                return false;
            }
        }
    }
    #endregion
}