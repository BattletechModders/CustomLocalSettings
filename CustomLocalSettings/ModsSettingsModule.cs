using BattleTech;
using BattleTech.Data;
using BattleTech.UI;
using BattleTech.UI.TMProWrapper;
using BattleTech.UI.Tooltips;
using DG.Tweening;
using HarmonyLib;
using Localize;
using SVGImporter;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

namespace CustomSettings {
  [System.AttributeUsage(System.AttributeTargets.Property, AllowMultiple = false)]
  public class LocalSettingValues : System.Attribute {
    public List<object> values { get; protected set; }
    public LocalSettingValues(params object[] values) { this.values = new List<object>(values); }
    public LocalSettingValues(Type type, string member) {
      this.values = type.GetMethod(member, BindingFlags.Static | BindingFlags.Public).Invoke(null, new object[0]) as List<object>;
    }
  }
  [System.AttributeUsage(System.AttributeTargets.Property, AllowMultiple = false)]
  public class NextSettingValue : System.Attribute {
    public virtual void Next(object settings) { return; }
    public NextSettingValue() { }
  }
  [System.AttributeUsage(System.AttributeTargets.Property, AllowMultiple = false)]
  public class NextSettingValueUI : System.Attribute {
    public virtual void Next(object settings, ModSettingElement ui) { return; }
    public virtual void OnSave(ModSettingElement ui) { return; }
    public NextSettingValueUI() { }
  }
  [System.AttributeUsage(System.AttributeTargets.Property, AllowMultiple = true)]
  public class LocalSettingValuesNames : System.Attribute {
    public Strings.Culture culture { get; protected set; }
    public List<string> names { get; protected set; }
    public LocalSettingValuesNames(Strings.Culture culture, params string[] names) { this.culture = culture; this.names = new List<string>(names); }
    public LocalSettingValuesNames(Strings.Culture culture, Func<Strings.Culture, List<string>> names) { this.culture = culture; this.names = names(culture); }
  }
  [System.AttributeUsage(System.AttributeTargets.Property, AllowMultiple = true)]
  public class LocalSettingName : System.Attribute {
    public Strings.Culture culture { get; protected set; }
    public string name { get; protected set; }
    public LocalSettingName(Strings.Culture culture, string name) { this.culture = culture; this.name = name; }
  }
  [System.AttributeUsage(System.AttributeTargets.Property, AllowMultiple = true)]
  public class LocalSettingDescription : System.Attribute {
    public Strings.Culture culture { get; protected set; }
    public string description {
      get {
        if (f_description != null) { return f_description; }
        if (descrType == null) { return string.Empty; }
        if (string.IsNullOrEmpty(descrMethod)) { return string.Empty; }
        try {
          f_description = (string)descrType.GetMethod(descrMethod, BindingFlags.Static | BindingFlags.Public).Invoke(null, new object[] { this.culture });
        }catch(Exception e) {
          f_description = e.ToString();
        }
        return f_description;
      }
    }
    private string f_description = null;
    private Type descrType = null;
    private string descrMethod = string.Empty;
    public LocalSettingDescription(Strings.Culture culture, string description) { this.culture = culture; this.f_description = description; }
    public LocalSettingDescription(Strings.Culture culture, Type dt, string method) { this.culture = culture; this.f_description = null; this.descrType = dt; this.descrMethod = method; }
  }
  [System.AttributeUsage(System.AttributeTargets.Property, AllowMultiple = false)]
  public class IsLocalSetting : System.Attribute {
    public bool needRestart { get; set; } = false;
    public IsLocalSetting(bool nr = true) { needRestart = nr; }
  }
  [System.AttributeUsage(System.AttributeTargets.Property, AllowMultiple = false)]
  public class IsCacheResetNeeded : System.Attribute {
    public bool needReset { get; set; } = false;
    public IsCacheResetNeeded(bool nr = true) { needReset = nr; }
  }

  public static class LocalSettingsHelper {
    private static Dictionary<PropertyInfo, Dictionary<Strings.Culture, string>> LocalSettingName_cache = new Dictionary<PropertyInfo, Dictionary<Strings.Culture, string>>();
    private static Dictionary<PropertyInfo, Dictionary<Strings.Culture, StringBuilder>> LocalSettingDescription_cache = new Dictionary<PropertyInfo, Dictionary<Strings.Culture, StringBuilder>>();
    private static Dictionary<PropertyInfo, bool?> IsLocalSetting_cache = new Dictionary<PropertyInfo, bool?>();
    private static Dictionary<PropertyInfo, bool?> IsCacheReset_cache = new Dictionary<PropertyInfo, bool?>();
    private static Dictionary<PropertyInfo, Action<object>> NextActions_cache = new Dictionary<PropertyInfo, Action<object>>();
    private static Dictionary<PropertyInfo, Action<object, ModSettingElement>> NextActionsUI_cache = new Dictionary<PropertyInfo, Action<object, ModSettingElement>>();
    public static bool? isLocalSetting(this PropertyInfo prop) {
      if (IsLocalSetting_cache.TryGetValue(prop, out var result)) { return result; }
      result = new bool?();
      IsLocalSetting locSetting = prop.GetCustomAttribute<IsLocalSetting>();
      if (locSetting != null) { result = locSetting.needRestart; }
      IsLocalSetting_cache.Add(prop, result);
      return result;
    }
    public static bool? isCacheResetSetting(this PropertyInfo prop) {
      if (IsCacheReset_cache.TryGetValue(prop, out var result)) { return result; }
      result = new bool?();
      IsCacheResetNeeded locSetting = prop.GetCustomAttribute<IsCacheResetNeeded>();
      if (locSetting != null) { result = locSetting.needReset; } else { result = false; }
      IsCacheReset_cache.Add(prop, result);
      return result;
    }
    public static Action<object> NextAction(this PropertyInfo prop) {
      if (NextActions_cache.TryGetValue(prop, out var result)) { return result; }
      result = null;
      Log.M?.TWL(0,$"LocalSettingsHelper.NextAction {prop.Name}:{prop.PropertyType.GetType()}");
      foreach (var attr in prop.GetCustomAttributes()) {
        NextSettingValue next = attr as NextSettingValue;
        Log.M?.WL(1, $"attr {attr.GetType()}:{((next==null)?"null":"not null")}");
        if (next == null) { continue; }
        result = next.Next;
        break;
      }
      NextActions_cache.Add(prop, result);
      return result;
    }
    public static Action<object, ModSettingElement> NextActionUI(this PropertyInfo prop) {
      if (NextActionsUI_cache.TryGetValue(prop, out var result)) { return result; }
      result = null;
      Log.M?.TWL(0, $"LocalSettingsHelper.NextActionUI {prop.Name}:{prop.PropertyType.GetType()}");
      foreach (var attr in prop.GetCustomAttributes()) {
        NextSettingValueUI next = attr as NextSettingValueUI;
        Log.M?.WL(1, $"attr {attr.GetType()}:{((next == null) ? "null" : "not null")}");
        if (next == null) { continue; }
        result = next.Next;
        break;
      }
      NextActionsUI_cache.Add(prop, result);
      return result;
    }
    public static string GetLocalSettingName(this PropertyInfo prop) {
      if (LocalSettingName_cache.TryGetValue(prop, out var localized)) {
        if (localized.TryGetValue(Strings.CurrentCulture, out var result)) { return result; }
        if (localized.TryGetValue(Strings.Culture.CULTURE_EN_US, out result)) { return result; }
        return prop.Name;
      }
      localized = new Dictionary<Strings.Culture, string>();
      foreach (var attr in prop.GetCustomAttributes<LocalSettingName>()) {
        localized[attr.culture] = attr.name;
      }
      LocalSettingName_cache[prop] = localized;
      if (localized.TryGetValue(Strings.CurrentCulture, out var res)) { return res; }
      if (localized.TryGetValue(Strings.Culture.CULTURE_EN_US, out res)) { return res; }
      return prop.Name;
    }
    public static string GetLocalSettingDescription(this PropertyInfo prop) {
      if (LocalSettingDescription_cache.TryGetValue(prop, out var localized)) {
        if (localized.TryGetValue(Strings.CurrentCulture, out var result)) { return result.ToString(); }
        if (localized.TryGetValue(Strings.Culture.CULTURE_EN_US, out result)) { return result.ToString(); }
        return prop.Name;
      }
      localized = new Dictionary<Strings.Culture, StringBuilder>();
      foreach (var attr in prop.GetCustomAttributes<LocalSettingDescription>()) {
        if (localized.TryGetValue(attr.culture, out var descr) == false) { descr = new StringBuilder(); }
        descr.Append(attr.description);
        localized[attr.culture] = descr;
      }
      LocalSettingDescription_cache[prop] = localized;
      if (localized.TryGetValue(Strings.CurrentCulture, out var res)) { return res.ToString(); ; }
      if (localized.TryGetValue(Strings.Culture.CULTURE_EN_US, out res)) { return res.ToString(); ; }
      return string.Empty;
    }
  }

  [HarmonyPatch(typeof(DataManager))]
  [HarmonyPatch("PooledInstantiate")]
  [HarmonyPatch(MethodType.Normal)]
  [HarmonyPatch(new Type[] { typeof(string), typeof(BattleTechResourceType), typeof(Vector3?), typeof(Quaternion?), typeof(Transform) })]
  public static class DataManager_PooledInstantiate {
    public static void Postfix(DataManager __instance, string id, BattleTechResourceType resourceType, Vector3? position, Quaternion? rotation,Transform parent, ref GameObject __result) {
      try {
        if (__result != null){ return; }
        if (resourceType != BattleTechResourceType.UIModulePrefabs) { return; }
        if (id != ModsLocalSettings.prefabName) { return; }
        __result = __instance.PooledInstantiate("uixPrfPanl_KB_KeybindOptions", BattleTechResourceType.UIModulePrefabs, position, rotation, parent);
        if (__result == null) { return; }
        __result.name = ModsLocalSettings.prefabName;
        ModsLocalSettings settingsObj = __result.GetComponent<ModsLocalSettings>();
        if (settingsObj == null) {
          KeybindingMenu keybindingMenu = __result.GetComponent<KeybindingMenu>();
          if (keybindingMenu != null) {
            Dictionary<string, object> srcData = ModsLocalSettings.getSourceData(keybindingMenu);
            GameObject.DestroyImmediate(keybindingMenu);
            settingsObj = __result.AddComponent<ModsLocalSettings>();
            settingsObj.Init(srcData);
          }
        }

      } catch (Exception e) { Log.M_Err?.TWL(0,e.ToString(), true); }
    }
  }
  [UIModule.PrefabNameAttr("uixPrfLstItm_MS_Element")]
  public class ModSettingElement: UIModule {
    public LocalizableText actionLabel;
    public HBSButton[] actionButtons;
    public SVGImage[] buttonBackgrounds;
    private bool uiInited = false;
    public bool inited = false;
    public ModsLocalSettings parent { get; set; }
    public ModSettingsResetAction resetAction { get; set; } = null;
    public object settings { get; set; } = null;
    public object defsettings { get; set; } = null;
    public PropertyInfo property { get; set; } = null;
    public static Dictionary<string, object> getSourceData(KeyBindElement keybindElement) {
      Dictionary<string, object> result = new Dictionary<string, object>();
      result["actionLabel"] = keybindElement.actionLabel;
      result["actionKeyBindButtons"] = keybindElement.actionKeyBindButtons;
      result["representation"] = keybindElement.representation;
      result["occlusionLayer"] = keybindElement.occlusionLayer;
      result["persistent"] = keybindElement.persistent;
      result["sortBehavior"] = keybindElement.sortBehavior;
      result["singleton"] = keybindElement.singleton;
      result["onPooledAction"] = keybindElement.onPooledAction;
      result["lastClickedTime"] = keybindElement.lastClickedTime;
      result["messageCenter"] = keybindElement.messageCenter;
      result["uiManager"] = keybindElement.uiManager;
      result["cTrans"] = keybindElement.cTrans;
      result["cRTrans"] = keybindElement.cRTrans;
      result["audioObject"] = keybindElement.audioObject;
      result["minFramesBetweenUpdates"] = keybindElement.minFramesBetweenUpdates;
      result["maxFramesBetweenUpdates"] = keybindElement.maxFramesBetweenUpdates;
      result["currentFrameCountBetweenUpdates"] = keybindElement.currentFrameCountBetweenUpdates;
      result["countForNextUpdate"] = keybindElement.countForNextUpdate;
      result["ParentModule"] = keybindElement.ParentModule;
      result["IsSubModule"] = keybindElement.IsSubModule;
      return result;
    }
    protected void Init(Dictionary<string, object> src) {
      this.actionLabel = src["actionLabel"] as LocalizableText;
      this.actionButtons = src["actionKeyBindButtons"] as HBSButton[];
      this.representation = src["representation"] as GameObject;
      this.occlusionLayer = src["occlusionLayer"] as GameObject;
      this.sortBehavior = (SortingBehavior)src["sortBehavior"];
      this.singleton = (bool)src["singleton"];
      this.onPooledAction = src["onPooledAction"] as Action;
      this.lastClickedTime = (float)src["lastClickedTime"];
      this.messageCenter = src["messageCenter"] as MessageCenter;
      this.uiManager = src["messageCenter"] as UIManager;
      this.cTrans = src["cTrans"] as Transform;
      this.cRTrans = src["cRTrans"] as RectTransform;
      this.audioObject = src["audioObject"] as AkGameObj;
      this.minFramesBetweenUpdates = (int)src["minFramesBetweenUpdates"];
      this.maxFramesBetweenUpdates = (int)src["maxFramesBetweenUpdates"];
      this.currentFrameCountBetweenUpdates = (int)src["currentFrameCountBetweenUpdates"];
      this.ParentModule = src["ParentModule"] as UIModule;
      this.IsSubModule = (bool)src["IsSubModule"];
    }
    public void Init(Dictionary<string, object> src, ModSettingsResetAction ra, object defsettings, object settings, PropertyInfo prop) {
      this.settings = settings;
      this.defsettings = defsettings;
      this.resetAction = ra;
      this.property = prop;
      this.Init(src);
      string description = this.property.GetLocalSettingDescription();
      if(string.IsNullOrEmpty(description) == false) {
        HBSTooltip tooltip = this.gameObject.GetComponent<HBSTooltip>();
        if (tooltip == null) { tooltip = this.gameObject.AddComponent<HBSTooltip>(); }
        tooltip.SetDefaultStateData(new BaseDescriptionDef($"{this.resetAction.id}_{this.property.Name}",this.property.GetLocalSettingName(), description, string.Empty).GetTooltipStateData());
      }
      GameObject.DestroyImmediate(this.GetComponent<HBSRadioSet>());
      this.actionLabel.SetText(property.GetLocalSettingName());
      this.actionButtons[1].SetState(ButtonState.Enabled);
      this.actionButtons[1].gameObject.FindObject<SVGImage>("editIcon (1)").gameObject.SetActive(false);
      this.actionButtons[1].gameObject.GetComponentInChildren<LocalizableText>().SetText(this.StrSetting());
      this.actionButtons[1].gameObject.GetComponentInChildren<LocalizableText>().color = UIManager.Instance.UIColorRefs.black;
      this.actionButtons[1].OnClicked = new UnityEvent();
      this.actionButtons[1].OnClicked.AddListener((UnityAction)(() => {
        (this.actionButtons[1] as HBSToggle).SetToggled(false);
        this.NextSetting();
        this.UpdateValue();
        //ModsLocalSettingsHelper.ResetSettings(resetAction, true);
      }));

      this.actionButtons[0].gameObject.SetActive(false);
      this.actionButtons[0].gameObject.FindObject<SVGImage>("editIcon").gameObject.SetActive(false);
      this.actionButtons[0].gameObject.GetComponentInChildren<LocalizableText>().gameObject.SetActive(false);
      this.actionButtons[0].gameObject.FindObject<Transform>("Fill").gameObject.SetActive(false);
      //DOTweenAnimation[] animations = this.actionButtons[0].GetComponents<DOTweenAnimation>();
      //foreach (var anim in animations) { GameObject.DestroyImmediate(anim); }
      inited = true;
    }
    public void ResetSettings() {
      foreach(var el in this.parent.elements) {
        if (el.resetAction != this.resetAction) { continue; }
        if (el.defsettings == null) { continue; }
        if (el.settings == null) { continue; }
        if (el.property == null) { continue; }
        el.ResetSetting();
      }
    }
    public void ResetSetting() {
      this.property.SetValue(this.settings, this.property.GetValue(this.defsettings));
      this.UpdateValue();
    }
    public void Init(Dictionary<string, object> src, ModSettingsResetAction ra, object defsettings, object settings) {
      this.settings = settings;
      this.defsettings = defsettings;
      this.resetAction = ra;
      this.Init(src);      
      buttonBackgrounds = new SVGImage[actionButtons.Length];
      for(int t=0;t<actionButtons.Length;++t) {
        buttonBackgrounds[t] = actionButtons[t].GetComponent<SVGImage>();
        actionButtons[t].parentUIModule = this;
      }
      GameObject.DestroyImmediate(this.GetComponent<HBSRadioSet>());
      this.actionLabel.SetText(resetAction.ui);
      this.actionButtons[1].SetState(ButtonState.Enabled);
      this.actionButtons[1].gameObject.FindObject<SVGImage>("editIcon (1)").gameObject.SetActive(false);
      this.actionButtons[1].gameObject.GetComponentInChildren<LocalizableText>().SetText("Reset to default");
      this.actionButtons[1].gameObject.GetComponentInChildren<LocalizableText>().color = UIManager.Instance.UIColorRefs.black;
      this.actionButtons[1].OnClicked = new UnityEvent();
      this.actionButtons[1].OnClicked.AddListener((UnityAction)(() => {
        (this.actionButtons[1] as HBSToggle).SetToggled(false);
        try {
          this.ResetSettings();
          ModsLocalSettingsHelper.ResetSettings(resetAction, true);
        }catch(Exception e) {
          Log.M_Err?.TWL(0,e.ToString(),true);
        }
      }));

      this.actionButtons[0].gameObject.SetActive(true);
      this.actionButtons[0].gameObject.FindObject<SVGImage>("editIcon").gameObject.SetActive(false);
      this.actionButtons[0].gameObject.GetComponentInChildren<LocalizableText>().gameObject.SetActive(false);
      this.actionButtons[0].gameObject.FindObject<Transform>("Fill").gameObject.SetActive(false);
      //DOTweenAnimation[] animations = this.actionButtons[0].GetComponents<DOTweenAnimation>();
      //foreach (var anim in animations) { GameObject.DestroyImmediate(anim); }
      inited = true;
    }
    public void uiInit() {
      //this.uiInited = true;
      try {
        if (buttonBackgrounds == null) { return; }
        if (buttonBackgrounds.Length <= 0) { return; }
        if (buttonBackgrounds[0] != null) buttonBackgrounds[0].color = Color.clear;
      } catch (Exception) {

      }
      //Log.M?.TWL(0, $"ModSettingElement.uiInit {this.actionButtons[0].gameObject.GetComponent<SVGImage>().color}");
    }
    public override void Update() {
      base.Update();
      if (inited == false) { return; }
      if (uiInited == false) { this.uiInit(); }
    }
    public string StrSetting() {
      if (this.settings == null) { return "null"; }
      if (this.property == null) { return "null"; }
      if (this.property.PropertyType == typeof(bool)) {
        switch (Strings.CurrentCulture) {
          case Strings.Culture.CULTURE_RU_RU: return ((bool)this.property.GetValue(this.settings)) ? "Да" : "Нет";
          default: return ((bool)this.property.GetValue(this.settings)) ? "Yes" : "No";
        }
      }
      return this.property.GetValue(this.settings).ToString();
    }
    public void NextSetting() {
      Log.M?.TWL(0,$"ModSettingElement.NextSetting settings:{((this.settings == null)?"null":this.settings.GetType().ToString())} property:{((this.property == null) ? "null" : this.property.Name)}");
      try {
        if (this.settings == null) { return; }
        if (this.property == null) { return; }
        if (this.property.NextAction() != null) {
          this.property.NextAction().Invoke(this.settings);
        } else if (this.property.NextActionUI() != null) {
          this.property.NextActionUI().Invoke(this.settings, this);
        } else if (this.property.PropertyType == typeof(bool)) {
          this.property.SetValue(this.settings, !((bool)this.property.GetValue(this.settings)));
        }
        if (this.property.isLocalSetting().Value) { this.parent.restartNeeded = true; }
        if (this.property.isCacheResetSetting().Value) { this.parent.cacheResetNeeded = true; }
        this.parent.hasUnsavedChanges = true;
        Log.M?.WL(1, $"hasUnsavedChanges:{this.parent.hasUnsavedChanges}");
      }catch(Exception e) {
        Log.M?.TWL(0,e.ToString());
        UIManager.logger.LogException(e);
      }
    }
    public void UpdateValue() {
      this.actionButtons[1].gameObject.GetComponentInChildren<LocalizableText>().SetText(this.StrSetting());
    }
  }
  [UIModule.PrefabNameAttr("uixPrfPanl_STG_modSettingsModule")]
  public class ModsLocalSettings : SettingsModule {
    public static readonly string prefabName = "uixPrfPanl_STG_modSettingsModule";
    public GameObject settingListElementPrefab { get; set; } = null;
    public Transform settingsContentRoot { get; set; } = null;
    public List<ModSettingElement> elements { get; set; } = new List<ModSettingElement>();
    public static Dictionary<string, object> getSourceData(KeybindingMenu keybindingMenu) {
      Dictionary<string, object> result = new Dictionary<string, object>();
      result["keybindListElementPrefab"] = keybindingMenu.keybindListElementPrefab;
      result["keybindingsContentRoot"] = keybindingMenu.keybindingsContentRoot;
      result["cachedPos"] = keybindingMenu.cachedPos;
      result["posCached"] = keybindingMenu.posCached;
      result["representation"] = keybindingMenu.representation;
      result["occlusionLayer"] = keybindingMenu.occlusionLayer;
      result["persistent"] = keybindingMenu.persistent;
      result["sortBehavior"] = keybindingMenu.sortBehavior;
      result["singleton"] = keybindingMenu.singleton;
      result["onPooledAction"] = keybindingMenu.onPooledAction;
      result["lastClickedTime"] = keybindingMenu.lastClickedTime;
      result["messageCenter"] = keybindingMenu.messageCenter;
      result["uiManager"] = keybindingMenu.uiManager;
      result["cTrans"] = keybindingMenu.cTrans;
      result["cRTrans"] = keybindingMenu.cRTrans;
      result["audioObject"] = keybindingMenu.audioObject;
      result["minFramesBetweenUpdates"] = keybindingMenu.minFramesBetweenUpdates;
      result["maxFramesBetweenUpdates"] = keybindingMenu.maxFramesBetweenUpdates;
      result["currentFrameCountBetweenUpdates"] = keybindingMenu.currentFrameCountBetweenUpdates;
      result["countForNextUpdate"] = keybindingMenu.countForNextUpdate;
      result["ParentModule"] = keybindingMenu.ParentModule;
      result["IsSubModule"] = keybindingMenu.IsSubModule;
      return result;
    }
    public override string GetName() {
      return Strings.T("Mods Settings");
    }
    public override void DefaultSettings() {
      foreach(var el in elements) {
        if (el.property != null) { continue; }
        ModsLocalSettingsHelper.ResetSettings(el.resetAction, false);
        el.ResetSettings();
      }
      ModsLocalSettingsHelper.ResetSettingDialog(false);
    }
    public bool restartNeeded { get; set; } = false;
    public bool cacheResetNeeded { get; set; } = false;
    public bool hasUnsavedChanges { get; set; } = false;
    public override void SaveSettings() {
      Log.M?.TWL(0, $"ModsLocalSettings.SaveSettings restartNeeded:{restartNeeded} cacheResetNeeded:{cacheResetNeeded}");
      foreach (var el in elements) {
        if (el.property != null) { continue; }
        //if (el.resetAction.save == null) { continue; }
        //el.resetAction.save()
        Log.M?.WL(1, $"{el.resetAction.id}");
        ModsLocalSettingsHelper.SaveSettings(el.resetAction, el.settings);
      }
      this.hasUnsavedChanges = false;
      if (restartNeeded) {
        ModsLocalSettingsHelper.SaveSettingDialog(false);
        restartNeeded = false;
      }
      if (cacheResetNeeded) {
        foreach(var action in ModsLocalSettingsHelper.cacheResetDelegates) {
          Log.M?.WL(0, $"invoke {action.Key} {action.Value.Method.Name}");
          action.Value();
        }
      }
    }
    public override void RevertSettings() {
    }
    public override void OnPooled() {
      base.OnPooled();
      foreach (var el in elements) { GameObject.Destroy(el.gameObject); }
    }
    public override void Init() {
      base.Init();
      this.Visible = false;
    }
    public override bool HasUnsavedChanges() {
      Log.M?.TWL(0, $"ModsLocalSettings.HasUnsavedChanges {hasUnsavedChanges} restartNeeded:{restartNeeded}");
      return hasUnsavedChanges;
    }
    public override void InitSettings() {
      Log.M?.TWL(0,$"ModsLocalSettings.InitSettings");
      try {
        base.InitSettings();
        this.gameObject.FindObject<Transform>("keybind_titles").gameObject.SetActive(false);
        foreach (var action in ModsLocalSettingsHelper.resetActions) {
          object settings = null;
          if (action.Value.CurrentSettings != null) { settings = action.Value.CurrentSettings(); }
          object defsettings = null;
          if (action.Value.DefaultSettings != null) { defsettings = action.Value.DefaultSettings(); }
          Log.M?.WL(1, $"{action.Key} CurrentSettings:{(action.Value.CurrentSettings==null?"null":"not null")} DefaultSettings:{(action.Value.DefaultSettings == null ? "null" : "not null")}");
          GameObject resetElement = GameObject.Instantiate(this.settingListElementPrefab);
          resetElement.SetActive(true);
          resetElement.transform.SetParent(this.settingsContentRoot);
          resetElement.transform.localScale = Vector3.one;
          resetElement.transform.localPosition = Vector3.zero;
          resetElement.transform.SetAsLastSibling();
          ModSettingElement rts_el = resetElement.GetComponent<ModSettingElement>();
          if (rts_el == null) {
            KeyBindElement kb_el = resetElement.GetComponent<KeyBindElement>();
            if (kb_el != null) {
              Dictionary<string, object> srcdata = ModSettingElement.getSourceData(kb_el);
              GameObject.DestroyImmediate(kb_el);
              rts_el = resetElement.AddComponent<ModSettingElement>();
              rts_el.parent = this;
              rts_el.Init(srcdata, action.Value, defsettings, settings);
            }
          }
          this.elements.Add(rts_el);
          if((action.Value.CurrentSettings != null) && (action.Value.DefaultSettings != null)) {
            Log.M?.WL(2, $"{settings.GetType().Name}:{settings.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Length}");
            foreach (var prop in settings.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
              Log.M?.WL(3, $"{prop.Name}:{prop.isLocalSetting().HasValue}");
              if (prop.isLocalSetting().HasValue == false) { continue; }
              GameObject settingElement = GameObject.Instantiate(this.settingListElementPrefab);
              settingElement.SetActive(true);
              settingElement.transform.SetParent(this.settingsContentRoot);
              settingElement.transform.localScale = Vector3.one;
              settingElement.transform.localPosition = Vector3.zero;
              settingElement.transform.SetAsLastSibling();
              ModSettingElement ms_el = settingElement.GetComponent<ModSettingElement>();
              if (ms_el == null) {
                KeyBindElement kb_el = settingElement.GetComponent<KeyBindElement>();
                if (kb_el != null) {
                  Dictionary<string, object> srcdata = ModSettingElement.getSourceData(kb_el);
                  GameObject.DestroyImmediate(kb_el);
                  ms_el = settingElement.AddComponent<ModSettingElement>();
                  ms_el.parent = this;
                  ms_el.Init(srcdata,action.Value, defsettings, settings, prop);
                }
              }
              this.elements.Add(ms_el);
            }
          }
        }
      } catch(Exception e) {
        Log.M_Err?.TWL(0,e.ToString(),true);
      }
    }
    public void Init(Dictionary<string, object> src) {
      this.settingListElementPrefab = src["keybindListElementPrefab"] as GameObject;
      this.settingsContentRoot = src["keybindingsContentRoot"] as Transform;
      this.cachedPos = (Vector2)src["cachedPos"];
      this.posCached = (bool)src["posCached"];
      this.representation = src["representation"] as GameObject;
      this.occlusionLayer = src["occlusionLayer"] as GameObject;
      this.sortBehavior = (SortingBehavior)src["sortBehavior"];
      this.singleton = (bool)src["singleton"];
      this.onPooledAction = src["onPooledAction"] as Action;
      this.lastClickedTime = (float)src["lastClickedTime"];
      this.messageCenter = src["messageCenter"] as MessageCenter;
      this.uiManager = src["messageCenter"] as UIManager;
      this.cTrans = src["cTrans"] as Transform;
      this.cRTrans = src["cRTrans"] as RectTransform;
      this.audioObject = src["audioObject"] as AkGameObj;
      this.minFramesBetweenUpdates = (int)src["minFramesBetweenUpdates"];
      this.maxFramesBetweenUpdates = (int)src["maxFramesBetweenUpdates"];
      this.currentFrameCountBetweenUpdates = (int)src["currentFrameCountBetweenUpdates"];
      this.ParentModule = src["ParentModule"] as UIModule;
      this.IsSubModule = (bool)src["IsSubModule"];
    }
  }
}