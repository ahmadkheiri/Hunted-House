using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public sealed class LanternController : MonoBehaviour
{
    public Light lanternLight;
    public Renderer flame;
    [Header("Lantern output (gameplay approximation)")]
    [Range(10,200)] public float lumens=60;
    public float minimumLumens=10, maximumLumens=200, brightnessStep=10;
    [Range(1500,3000)] public float temperatureKelvin=2000;
    [Range(0,.15f)] public float flickerAmount=.05f;
    public float flickerSpeed=2;
    public float renderingRange=15;
    public bool isLit=true;
    [Header("Eye adaptation when global lighting is off")]
    public float darkExposureEV=-2;
    public float adaptationSeconds=2;
    public float ActualLumens { get; private set; }
    public static Rect PanelRect => new Rect(16,16,290,MoonLightingController.Current!=null?400:186);
    ChildFirstPersonController player;
    GlobalLightingToggle global;
    Volume volume;
    VolumeProfile profile;
    Exposure exposure;
    MaterialPropertyBlock properties;
    public static bool IsPointerOverPanel(Vector2 position) => PanelRect.Contains(new Vector2(position.x,Screen.height-position.y));
    void Awake()
    {
        player=GetComponent<ChildFirstPersonController>();global=GetComponent<GlobalLightingToggle>();
        properties=new MaterialPropertyBlock();
        var go=new GameObject("Lantern eye adaptation");go.transform.SetParent(transform,false);
        volume=go.AddComponent<Volume>();volume.isGlobal=true;volume.priority=10001;volume.weight=0;
        profile=ScriptableObject.CreateInstance<VolumeProfile>();volume.sharedProfile=profile;
        exposure=profile.Add<Exposure>(true);exposure.mode.Override(ExposureMode.Fixed);exposure.fixedExposure.Override(darkExposureEV);
        var bloom=profile.Add<Bloom>(false);bloom.intensity.Override(.025f);bloom.scatter.Override(.25f);
        var fog=profile.Add<Fog>(false);fog.enabled.Override(false);
        ApplyLight(0);
    }
    void Update()
    {
        if(player!=null && player.InputEnabled && Application.isFocused && Keyboard.current!=null)
        {
            var k=Keyboard.current;
            if(k.digit1Key.wasPressedThisFrame || k.numpad1Key.wasPressedThisFrame) AdjustBrightness(-1);
            if(k.digit2Key.wasPressedThisFrame || k.numpad2Key.wasPressedThisFrame) AdjustBrightness(1);
            if(k.fKey.wasPressedThisFrame) SetLit(!isLit);
        }
        ApplyLight(Time.time);
    }
    void LateUpdate()
    {
        exposure.fixedExposure.value=darkExposureEV;
        // Restore daylight exposure immediately, before bright scene lights render.
        volume.weight=global!=null&&!global.IsOn?Mathf.MoveTowards(volume.weight,1,Time.deltaTime/Mathf.Max(.1f,adaptationSeconds)):0;
    }
    public void AdjustBrightness(int direction) {lumens=Mathf.Clamp(lumens+direction*brightnessStep,minimumLumens,maximumLumens);ApplyLight(Time.time);}
    public void SetLit(bool value) {isLit=value;ApplyLight(Time.time);}
    public void ApplyLight(float time)
    {
        if(lanternLight==null)return;
        lumens=Mathf.Clamp(lumens,minimumLumens,maximumLumens);
        float flicker=1+(Mathf.PerlinNoise(time*flickerSpeed,8.17f)*2-1)*flickerAmount;
        ActualLumens=isLit?lumens*flicker:0;
        lanternLight.type=LightType.Point;lanternLight.lightUnit=LightUnit.Lumen;
        lanternLight.intensity=LightUnitUtils.ConvertIntensity(lanternLight,ActualLumens,LightUnit.Lumen,LightUnitUtils.GetNativeLightUnit(LightType.Point));
        lanternLight.range=renderingRange;lanternLight.color=Color.white;
        lanternLight.useColorTemperature=true;lanternLight.colorTemperature=temperatureKelvin;lanternLight.enabled=isLit;
        if(flame!=null)
        {
            flame.enabled=isLit;
            if(properties==null)properties=new MaterialPropertyBlock();
            properties.SetColor("_EmissiveColor",Mathf.CorrelatedColorTemperatureToRGB(temperatureKelvin)*Mathf.Max(1,ActualLumens)*.3f);
            flame.SetPropertyBlock(properties);
        }
    }
    void OnGUI()
    {
        if(player!=null&&!player.InputEnabled)return;
        GUI.Box(PanelRect,"");
        GUILayout.BeginArea(new Rect(28,24,266,PanelRect.height-16));
        GUILayout.Label("LANTERN   •   "+(isLit?"ON":"OFF")+"   [F]");
        GUILayout.Label($"Output: {lumens:0} lm   •   Flame: {temperatureKelvin:0} K");
        GUILayout.Label($"Live output: {ActualLumens:0.0} lm");
        GUILayout.Label($"Ideal at 1 m: {ActualLumens/(4*Mathf.PI):0.00} lux");
        GUILayout.Label($"Flicker: ±{flickerAmount*100:0}%   •   Cutoff: {renderingRange:0} m");
        bool enabled=GUI.enabled;GUI.enabled=Cursor.lockState!=CursorLockMode.Locked;
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("1  Dimmer",GUILayout.Height(28)))AdjustBrightness(-1);
        if(GUILayout.Button("2  Brighter",GUILayout.Height(28)))AdjustBrightness(1);
        GUILayout.EndHorizontal();GUI.enabled=enabled;
        GUILayout.Label("1 / 2 adjust • Esc to click");
        if(MoonLightingController.Current!=null){GUI.enabled=Cursor.lockState!=CursorLockMode.Locked;MoonLightingController.Current.DrawControls();GUI.enabled=enabled;}
        GUILayout.EndArea();
    }
    void OnDisable(){if(volume!=null)volume.weight=0;if(lanternLight!=null)lanternLight.enabled=false;}
    void OnDestroy(){if(profile!=null)Destroy(profile);}
}
