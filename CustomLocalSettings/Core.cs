using BattleTech;
using BattleTech.Save;
using BattleTech.Save.Core;
using BattleTech.UI;
using BattleTech.UI.TMProWrapper;
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CustomSettings{
  [HarmonyPatch(typeof(SaveManager))]
  [HarmonyPatch(MethodType.Constructor)]
  [HarmonyPatch(new Type[] { typeof(MessageCenter) })]
  public static class SaveManager_Constructor {
    public static void Postfix(SaveManager __instance) {
      Log.M?.TWL(0, "SaveManager:" + Traverse.Create(Traverse.Create(Traverse.Create(__instance).Field<SaveSystem>("saveSystem").Value).Field<WriteLocation>("localWriteLocation").Value).Field<string>("rootPath").Value);
      //FixedMechDefHelper.Init(Path.GetDirectoryName(Traverse.Create(Traverse.Create(Traverse.Create(__instance).Field<SaveSystem>("saveSystem").Value).Field<WriteLocation>("localWriteLocation").Value).Field<string>("rootPath").Value));
      ModsLocalSettingsHelper.Init(Path.GetDirectoryName(Traverse.Create(Traverse.Create(Traverse.Create(__instance).Field<SaveSystem>("saveSystem").Value).Field<WriteLocation>("localWriteLocation").Value).Field<string>("rootPath").Value));
    }
  }
  //public class ModsLocalSettingsDublicator : MonoBehaviour {
  //  public void Update() {
  //    if (ModsLocalSettingsHelper.settingsWindow != null) { return; }
  //    ModsLocalSettingsHelper.settingsWindow = GameObject.Instantiate(this.gameObject);
  //    ModsLocalSettingsHelper.settingsWindow.gameObject.name = "uixPrfPanl_STG_modSettingsModule(Clone)";
  //    ModsLocalSettingsHelper.settingsWindow.transform.SetParent(this.gameObject.transform.parent);
  //    ModsLocalSettingsHelper.settingsWindow.transform.SetSiblingIndex(this.transform.GetSiblingIndex() + 1);
  //    GameSettingsModule gameSettingsModule = ModsLocalSettingsHelper.settingsWindow.gameObject.GetComponent<GameSettingsModule>();
  //    if (gameSettingsModule != null) GameObject.DestroyImmediate(gameSettingsModule);
  //    ModsLocalSettingsDublicator dublicator = ModsLocalSettingsHelper.settingsWindow.gameObject.GetComponent<ModsLocalSettingsDublicator>();
  //    if (dublicator != null) GameObject.DestroyImmediate(dublicator);
  //    ModsLocalSettingsHelper.settingsWindow.transform.localScale = this.transform.localScale;
  //    ModsLocalSettingsHelper.settingsWindow.transform.localPosition = this.transform.localPosition;
  //    ModsLocalSettingsHelper.settingsWindow.transform.localRotation = this.transform.localRotation;
  //    ModsLocalSettingsHelper.settingsWindow.SetActive(false);
  //    VerticalLayoutGroup Content = ModsLocalSettingsHelper.settingsWindow.FindObject<VerticalLayoutGroup>("Content");
  //    if (Content != null) { GameObject.DestroyImmediate(Content.gameObject); }
  //    ScrollRect gameplay_scroll = ModsLocalSettingsHelper.settingsWindow.FindObject<ScrollRect>("gameplay_scroll");
  //    if ((gameplay_scroll != null) && (ModsLocalSettingsHelper.menuButtons != null)) {
  //      GameObject menuButtons = GameObject.Instantiate(ModsLocalSettingsHelper.menuButtons);
  //      ContentSizeFitter contentSizeFitter = menuButtons.GetComponent<ContentSizeFitter>();
  //      contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
  //      contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
  //      gameplay_scroll.content = menuButtons.transform as RectTransform;
  //      menuButtons.name = "mod_settings_reset_buttons";
  //      menuButtons.transform.SetParent(gameplay_scroll.viewport);
  //      menuButtons.transform.localPosition = Vector3.zero;
  //      menuButtons.transform.localRotation = Quaternion.identity;
  //      menuButtons.transform.localScale = Vector3.one;
  //      menuButtons.SetActive(true);
  //      HBSDOTweenToggle[] buttons = menuButtons.GetComponentsInChildren<HBSDOTweenToggle>(true);
  //      for (int t = 1; t < buttons.Length; ++t) {
  //        menuButtons.GetComponent<HBSRadioSet>().RemoveButtonFromRadioSet(buttons[t]);
  //        GameObject.DestroyImmediate(buttons[t].gameObject);
  //      }
  //      buttons[0].gameObject.name = "uixPrfBttn_BASE_TabMedium-mod_Inital";
  //      LocalizableText tab_text_off = buttons[0].gameObject.FindObject<LocalizableText>("tab_text_off");
  //      if (tab_text_off != null) { tab_text_off.SetText("PLACEHOLDER"); }
  //      buttons[0].OnClicked = new UnityEvent();
  //      buttons[0].gameObject.SetActive(false);
  //      foreach (var action in ModsLocalSettingsHelper.resetActions) {
  //        HBSDOTweenToggle mod_button = GameObject.Instantiate(buttons[0].gameObject, buttons[0].gameObject.transform.parent).GetComponent<HBSDOTweenToggle>();
  //        mod_button.gameObject.name = "uixPrfBttn_BASE_TabMedium-mod" + action.Key;
  //        menuButtons.GetComponent<HBSRadioSet>().AddButtonToRadioSet(mod_button);
  //        tab_text_off = mod_button.gameObject.FindObject<LocalizableText>("tab_text_off");
  //        if (tab_text_off != null) { tab_text_off.SetText(action.Value.ui); }
  //        mod_button.OnClicked = new UnityEvent();
  //        mod_button.OnClicked.AddListener((UnityAction)(() => { ModsLocalSettingsHelper.ResetSettings(action.Value, true); }));
  //        mod_button.gameObject.SetActive(true);
  //      }
  //    }
  //  }
  //}
  public class ModSettingsResetAction {
    public string id { get; set; }
    public string ui { get; set; }
    public Action<string> read { get; set; }
    public Func<string> reset { get; set; }
    public Func<object, string> save { get; set; }
    public Func<object> DefaultSettings { get; set; }
    public Func<object> CurrentSettings { get; set; }
  }
  public static class ModsLocalSettingsHelper {
    public static GameObject menuButtons = null;
    public static string directory { get; private set; }
    public static Dictionary<string, Action> cacheResetDelegates = new Dictionary<string, Action>();
    public static void ResetSettingDialog(bool error) {
      if (error == false) {
        GenericPopup popup = GenericPopupBuilder.Create("RESET SETTINGS", "Restart game for settings to take effect").IsNestedPopupWithBuiltInFader().CancelOnEscape().Render();
      } else {
        GenericPopup popup = GenericPopupBuilder.Create("RESET SETTINGS", "Something went wrong. Refer log").IsNestedPopupWithBuiltInFader().CancelOnEscape().Render();
      }
    }
    public static void SaveSettingDialog(bool error) {
      if (error == false) {
        GenericPopup popup = GenericPopupBuilder.Create("SAVE SETTINGS", "Restart game for settings to take effect").IsNestedPopupWithBuiltInFader().CancelOnEscape().Render();
      } else {
        GenericPopup popup = GenericPopupBuilder.Create("SAVE SETTINGS", "Something went wrong. Refer log").IsNestedPopupWithBuiltInFader().CancelOnEscape().Render();
      }
    }
    public static void ResetSettings(ModSettingsResetAction action, bool showPopup) {
      try {
        string resetContent = action.reset();
        string filename = Path.Combine(directory, action.id + ".json");
        File.WriteAllText(filename, resetContent);
        if (showPopup) {
          ResetSettingDialog(false);
        }
      } catch (Exception e) {
        Log.M.TWL(0, e.ToString(), true);
        if (showPopup) {
          ResetSettingDialog(true);
        }
      }
    }
    public static void SaveSettings(ModSettingsResetAction action, object settings) {
      try {
        if (action.save == null) { return; }
        string savedContent = action.save(settings);
        string filename = Path.Combine(directory, action.id + ".json");
        File.WriteAllText(filename, savedContent);
      } catch (Exception e) {
        Log.M.TWL(0, e.ToString(), true);
      }
    }
    public static void SaveSettings(string id) {
      if(resetActions.TryGetValue(id, out var action)) {
        SaveSettings(action, action.CurrentSettings());
      }
    }
    public static void Init(string dir) {
      ModsLocalSettingsHelper.directory = Path.Combine(dir, "modsettings");
      if (Directory.Exists(ModsLocalSettingsHelper.directory) == false) { Directory.CreateDirectory(ModsLocalSettingsHelper.directory); }
      foreach (var action in ModsLocalSettingsHelper.resetActions) {
        string filename = Path.Combine(directory, action.Value.id + ".json");
        if (File.Exists(filename)) {
          if(Core.Settings.useLocalSettings) action.Value.read(File.ReadAllText(filename));
        } else {
          ResetSettings(action.Value, false);
        }
      }
    }
    //public static string ResetSettings() {
    //  return CustomAmmoCategories.GlobalSettings.SerializeLocal();
    //}
    //public static void ReadSettings(string json) {
    //  try {
    //    Settings local = JsonConvert.DeserializeObject<Settings>(json);
    //    CustomAmmoCategories.Settings.ApplyLocal(local);
    //  } catch (Exception e) {
    //    Log.M.TWL(0, e.ToString(), true);
    //  }
    //}
    public static T FindObject<T>(this GameObject go, string name) where T : Component {
      T[] arr = go.GetComponentsInChildren<T>(true);
      foreach (T component in arr) { if (component.gameObject.transform.name == name) { return component; } }
      return null;
    }
    public static Dictionary<string, ModSettingsResetAction> resetActions = new Dictionary<string, ModSettingsResetAction>();
    public static void RegisterLocalSettings(string ID, string UI, Func<string> resetDelegate, Action<string> readDelegate) {
      if (resetActions.ContainsKey(ID)) {
        resetActions[ID] = new ModSettingsResetAction() { id = ID, ui = UI, read = readDelegate, reset = resetDelegate, DefaultSettings = null, CurrentSettings = null };
      } else {
        resetActions.Add(ID, new ModSettingsResetAction() { id = ID, ui = UI, read = readDelegate, reset = resetDelegate, DefaultSettings = null, CurrentSettings = null });
      }
    }
    public static void RegisterLocalSettings(string ID, string UI, Func<string> resetDelegate, Action<string> readDelegate, Func<object> defset, Func<object> curset, Func<object,string> sv) {
      if (resetActions.ContainsKey(ID)) {
        resetActions[ID] = new ModSettingsResetAction() { id = ID, ui = UI, read = readDelegate, reset = resetDelegate, DefaultSettings = defset, CurrentSettings = curset, save = sv };
      } else {
        resetActions.Add(ID, new ModSettingsResetAction() { id = ID, ui = UI, read = readDelegate, reset = resetDelegate, DefaultSettings = defset, CurrentSettings = curset, save = sv });
      }
    }
    public static void RegisterCacheResetDelegate(string ID, Action onCacheReset) {
      cacheResetDelegates[ID] = onCacheReset;
    }
    public static GameObject settingsWindow { get; set; } = null;
    public static void Instantine() {
      //if (settingsWindow != null) { return; }
      //settingsWindow = UIManager.Instance.dataManager.PooledInstantiate("uixPrfPanl_KB_KeybindOptions", BattleTechResourceType.UIModulePrefabs); ;
    }
    public static void ShowSettingsWindow(SettingsMenu menu, ref LocalizableText currentModuleTitle) {
      if(settingsWindow == null) { ModsLocalSettingsHelper.Instantine(); }
      if (settingsWindow != null) {
        settingsWindow.transform.SetSiblingIndex(menu.gameObject.transform.GetSiblingIndex() + 1);
        Log.M.TWL(0, "ShowSettingsWindow.settingsWindow show " + menu.gameObject.transform.GetSiblingIndex() + " " + settingsWindow.transform.GetSiblingIndex());
        settingsWindow.SetActive(true); currentModuleTitle.SetText("MODS SETTINGS");
        return;
      }
    }
    public static void HideSettingsWindow() {
      if (settingsWindow != null) { settingsWindow.SetActive(false); }
    }
  }
  //[HarmonyPatch(typeof(GameSettingsModule))]
  //[HarmonyPatch("InitSettings")]
  //[HarmonyPatch(MethodType.Normal)]
  //[HarmonyPatch(new Type[] { })]
  //public static class GameSettingsModule_InitSettings {
  //  public static void Prefix(GameSettingsModule __instance) {
  //    ModsLocalSettingsDublicator dublicatior = __instance.gameObject.GetComponent<ModsLocalSettingsDublicator>();
  //    if (dublicatior == null) { dublicatior = __instance.gameObject.AddComponent<ModsLocalSettingsDublicator>(); }
  //    Log.M.TWL(0, "GameSettingsModule.InitSettings:" + (dublicatior == null ? "null" : "not null"));
  //  }
  //}
  [HarmonyPatch(typeof(SettingsMenu))]
  [HarmonyPatch("ReceiveButtonPress")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { typeof(string) })]
  public static class SettingsMenu_ReceiveButtonPress {
    public static void Postfix(SettingsMenu __instance, ref string button, ref LocalizableText ___currentModuleTitle, ref ISettingsModule ____activeModule) {
      if (button == "modsettings") {
        __instance.ShowModule<ModsLocalSettings>();
        //Log.M.TWL(0, "SettingsMenu.ReceiveButtonPress mod settings", true);
        //if (____activeModule != null) {
        //  ____activeModule.CacheSettings();
        //  ____activeModule.Visible = false;
        //  ____activeModule = (ISettingsModule)null;
        //}
        //ModsLocalSettingsHelper.ShowSettingsWindow(__instance, ref ___currentModuleTitle);
        //return false;
      }
      //ModsLocalSettingsHelper.HideSettingsWindow();
      //Log.M.TWL(0, "SettingsMenu.ReceiveButtonPress:" + button + "\n" + Environment.StackTrace.ToString(), true);
      //return true;
    }
  }
  //[HarmonyPatch(typeof(HBSToggle))]
  //[HarmonyPatch("OnPointerUp")]
  //[HarmonyPatch(MethodType.Normal)]
  //[HarmonyPatch(new Type[] { typeof(PointerEventData) })]
  //public static class HBSToggle_OnPointerUp {
  //  public static bool Prefix(HBSToggle __instance, PointerEventData eventData) {
  //    //Log.M.TWL(0, "HBSToggle.OnPointerUp:" + __instance.gameObject.name, true);
  //    return true;
  //  }
  //}

  [HarmonyPatch(typeof(SettingsMenu))]
  [HarmonyPatch("OnAddedToHierarchy")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { })]
  public static class SettingsMenu_OnAddedToHierarchy {
    public static void Postfix(SettingsMenu __instance) {
      //Log.M.TWL(0, "SettingsMenu.ReceiveButtonPress:" + Environment.StackTrace.ToString(), true);
      Log.M.TWL(0, "SettingsMenu.OnAddedToHierarchy:", true);
      if (ModsLocalSettingsHelper.settingsWindow != null) {
        GameObject.DestroyImmediate(ModsLocalSettingsHelper.settingsWindow);
        ModsLocalSettingsHelper.settingsWindow = null;
      }
      HBSRadioSet[] radioSets = __instance.gameObject.GetComponentsInChildren<HBSRadioSet>(true);
      HBSRadioSet menuButtons = null;
      foreach (HBSRadioSet radioSet in radioSets) {
        if (radioSet.gameObject.transform.name == "menuButtons") { menuButtons = radioSet; break; }
      }
      if (menuButtons != null) {
        if (ModsLocalSettingsHelper.menuButtons == null) {
          ModsLocalSettingsHelper.menuButtons = GameObject.Instantiate(menuButtons.gameObject);
          ModsLocalSettingsHelper.menuButtons.SetActive(false);
        }
        HBSDOTweenToggle mod_button = null;
        HBSDOTweenToggle source_button = null;
        foreach (HBSButton btn in menuButtons.RadioButtons) {
          HBSDOTweenToggle tbtn = btn as HBSDOTweenToggle;
          if (tbtn == null) { continue; }
          if (tbtn.gameObject.transform.name == "uixPrfBttn_BASE_TabMedium-mod") { mod_button = tbtn; break; }
          if (tbtn.gameObject.transform.name == "uixPrfBttn_BASE_TabMedium-game") { source_button = tbtn; }
        }
        if ((mod_button == null) && (source_button != null)) {
          mod_button = GameObject.Instantiate(source_button.gameObject, source_button.gameObject.transform.parent).GetComponent<HBSDOTweenToggle>();
          mod_button.gameObject.name = "uixPrfBttn_BASE_TabMedium-mod";
          menuButtons.AddButtonToRadioSet(mod_button);
          //mod_button.OnClicked.RemoveAllListeners();
          mod_button.OnClicked = new UnityEvent();
          mod_button.OnClicked.AddListener((UnityAction)(() => { mod_button.parentUIModule.ReceiveButtonPress("modsettings"); }));
          LocalizableText tab_text_off = mod_button.gameObject.FindObject<LocalizableText>("tab_text_off");
          if (tab_text_off != null) { tab_text_off.SetText("MODS SETTINGS"); }
        }
      }
    }
  }
  public class CSSettings {
    public bool debugLog { get; set; } = false;
    public bool useLocalSettings { get; set; } = true;
    [JsonIgnore]
    public string directory { get; set; } = string.Empty;
  }
  public static class Core { 
    public static CSSettings Settings { get; set; }
    public static void FinishedLoading(List<string> loadOrder) {
      Log.M_Err?.TWL(0, "FinishedLoading", true);
    }
    public static void Init(string directory, string settingsJson) {
      Log.BaseDirectory = directory;
      Core.Settings = new CSSettings();
      Core.Settings.directory = directory;
      Log.InitLog();
      try {
        Log.M_Err?.TWL(0, "Initing... " + directory + " version: " + Assembly.GetExecutingAssembly().GetName().Version, true);
        Core.Settings = JsonConvert.DeserializeObject<CSSettings>(settingsJson);
        Core.Settings.directory = directory;
        try {
          var harmony = new Harmony("io.kmission.localsettings");
          harmony.PatchAll(Assembly.GetExecutingAssembly());
        } catch (Exception e) {
          Log.M.TWL(0, e.ToString(), true);
        }
      } catch (Exception e) {
        Log.M_Err?.TWL(0, e.ToString(), true);
      }
    }
  }
}
